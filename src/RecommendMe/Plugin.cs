using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using RecommendMe.Configuration;
using RecommendMe.Services;
using RecommendMe.Storage;
using RecommendMe.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RecommendMe
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages, IServerEntryPoint
    {
        private static readonly TimeSpan BackgroundShutdownTimeout = TimeSpan.FromSeconds(10);

        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;
        private readonly IUserDataManager userDataManager;
        private readonly object lifecycleLock = new object();
        private readonly HashSet<Task> backgroundTasks = new HashSet<Task>();

        private List<IPluginUIPageController> pages;
        private bool isRunning;
        private bool isDisposed;

        public Plugin(
            IServerApplicationHost applicationHost,
            ILogManager logManager)
            : base(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IXmlSerializer>())
        {
            this.applicationHost = applicationHost;
            this.logger = logManager.GetLogger(this.Name);

            // --- Storage layer -------------------------------------------------
            var appPaths = applicationHost.Resolve<IApplicationPaths>();
            var fileSystem = applicationHost.Resolve<IFileSystem>();
            var jsonSerializer = applicationHost.Resolve<IJsonSerializer>();

            this.AdminSettingsStore = new AdminSettingsStore(appPaths, fileSystem, jsonSerializer, this.logger);
            this.RecommendationStore = new RecommendationStore(appPaths, fileSystem, jsonSerializer, this.logger);
            this.UserPreferenceStore = new UserPreferenceStore(appPaths, fileSystem, jsonSerializer, this.logger);
            this.CollectionRegistryStore = new CollectionRegistryStore(appPaths, fileSystem, jsonSerializer, this.logger);

            // --- Service layer ---------------------------------------------------
            this.UserManager = applicationHost.Resolve<IUserManager>();
            this.LibraryManager = applicationHost.Resolve<ILibraryManager>();
            this.userDataManager = applicationHost.Resolve<IUserDataManager>();

            this.PermissionService = new PermissionService(this.AdminSettingsStore, this.UserPreferenceStore);

            this.CollectionSyncService = new CollectionSyncService(
                applicationHost.Resolve<ICollectionManager>(),
                this.LibraryManager,
                this.CollectionRegistryStore,
                this.logger);

            this.NotificationService = new NotificationService(applicationHost.Resolve<ISessionManager>());

            this.RecommendationService = new RecommendationService(
                this.PermissionService,
                this.CollectionSyncService,
                this.NotificationService,
                this.RecommendationStore,
                this.AdminSettingsStore,
                this.userDataManager,
                this.logger);

            // Publish only a fully constructed instance. If construction of a
            // store or service fails, UI and scheduled-task code must not see
            // a partially initialized plugin through the static bridge.
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        // Exposed so UI controllers/commands (which aren't DI-constructed by
        // Emby) can reach the same singleton service instances.
        public AdminSettingsStore AdminSettingsStore { get; }

        public RecommendationStore RecommendationStore { get; }

        public UserPreferenceStore UserPreferenceStore { get; }

        public CollectionRegistryStore CollectionRegistryStore { get; }

        public PermissionService PermissionService { get; }

        public CollectionSyncService CollectionSyncService { get; }

        public NotificationService NotificationService { get; }

        public RecommendationService RecommendationService { get; }

        public IUserManager UserManager { get; }

        public ILibraryManager LibraryManager { get; }

        /// <summary>Exposed for UI-layer code (e.g. HistoryViewBuilder) that isn't DI-constructed and has no other route to the plugin's logger.</summary>
        public ILogger Logger => this.logger;

        /// <summary>
        /// Convenience wrapper around the non-obsolete GetUserList(UserQuery)
        /// API - IUserManager.Users itself is obsolete (flagged for "avoid
        /// working with the entire user list all at once"), but this plugin's
        /// admin matrix / target-user dropdowns genuinely do need the full
        /// list. An empty UserQuery returns all users.
        /// </summary>
        public IReadOnlyList<User> GetAllUsers() =>
            this.UserManager.GetUserList(new UserQuery());

        public override string Description =>
            "Lets users recommend movies, shows, and music to each other via native Emby Collections.";

        public override Guid Id =>
            new Guid("1E0C5960-DF19-4C22-AF9A-FA0FDC3EF649");

        public override string Name =>
            "RecommendMe";

        public ImageFormat ThumbImageFormat =>
            ImageFormat.Png;

        public Stream GetThumbImage()
            => this.GetType()
                .Assembly
                .GetManifestResourceStream(
                    this.GetType().Namespace + ".thumb.png");

        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (this.pages == null)
                {
                    this.pages = new List<IPluginUIPageController>();

                    this.pages.Add(
                        new AdminPageController(
                            this.GetPluginInfo(),
                            this.applicationHost,
                            this.logger));

                    this.pages.Add(
                        new UserPageController(
                            this.GetPluginInfo(),
                            this.applicationHost,
                            this.logger));
                }

                return this.pages.AsReadOnly();
            }
        }

        /// <summary>
        /// Starts the event-driven portion of the plugin. Emby invokes this
        /// through <see cref="IServerEntryPoint"/> after all server parts have
        /// been constructed, and later invokes <see cref="Dispose"/> during
        /// shutdown.
        /// </summary>
        public void Run()
        {
            lock (this.lifecycleLock)
            {
                if (this.isDisposed || this.isRunning)
                {
                    return;
                }

                this.userDataManager.UserDataSaved += this.OnUserDataSaved;
                this.isRunning = true;
            }
        }

        /// <summary>
        /// Stops accepting playback events and gives already-started cleanup
        /// work a bounded opportunity to finish before Emby disposes the
        /// services on which that work depends.
        /// </summary>
        public void Dispose()
        {
            Task[] tasks;

            lock (this.lifecycleLock)
            {
                if (this.isDisposed)
                {
                    return;
                }

                this.isDisposed = true;

                if (this.isRunning)
                {
                    this.userDataManager.UserDataSaved -= this.OnUserDataSaved;
                    this.isRunning = false;
                }

                tasks = new Task[this.backgroundTasks.Count];
                this.backgroundTasks.CopyTo(tasks);
            }

            if (tasks.Length > 0)
            {
                try
                {
                    if (!Task.WaitAll(tasks, BackgroundShutdownTimeout))
                    {
                        this.logger.Warn(
                            "Timed out waiting for {0} watched-item cleanup task(s) during plugin shutdown.",
                            tasks.Length);
                    }
                }
                catch (AggregateException ex)
                {
                    // The worker normally observes and logs its own failures.
                    // Retain this guard so teardown itself can never fail if a
                    // task faults before entering that worker.
                    this.logger.ErrorException(
                        "Error waiting for watched-item cleanup during plugin shutdown",
                        ex.Flatten());
                }
            }

            if (ReferenceEquals(Instance, this))
            {
                Instance = null!;
            }
        }

        private void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            if (e?.User == null || e.Item == null || e.UserData == null || !e.UserData.Played)
            {
                return;
            }

            Task task;

            lock (this.lifecycleLock)
            {
                if (this.isDisposed || !this.isRunning)
                {
                    return;
                }

                // Do not block Emby's user-data pipeline on file or collection
                // I/O. Register the task while holding the lifecycle lock so
                // Dispose cannot miss work that has already been accepted.
                task = Task.Run(() => this.HandleItemWatchedAsync(
                    e.User.InternalId,
                    e.Item.InternalId,
                    e.User));
                this.backgroundTasks.Add(task);
            }

            _ = task.ContinueWith(
                completedTask =>
                {
                    lock (this.lifecycleLock)
                    {
                        this.backgroundTasks.Remove(completedTask);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task HandleItemWatchedAsync(long userId, long itemId, User user)
        {
            try
            {
                if (this.IsDisposed())
                {
                    return;
                }

                var settings = await this.AdminSettingsStore.GetAsync().ConfigureAwait(false);
                if (!settings.ClearWatchedRecommendations || this.IsDisposed())
                {
                    return;
                }

                await this.RecommendationService
                    .HandleItemWatchedAsync(userId, itemId, user)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error handling watched-item cleanup", ex);
            }
        }

        private bool IsDisposed()
        {
            lock (this.lifecycleLock)
            {
                return this.isDisposed;
            }
        }
    }
}
