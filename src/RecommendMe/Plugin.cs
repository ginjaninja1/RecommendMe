using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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
using System.Threading.Tasks;

namespace RecommendMe
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages
    {
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;
        private readonly IUserDataManager userDataManager;

        private List<IPluginUIPageController> pages;

        public Plugin(
            IServerApplicationHost applicationHost,
            ILogManager logManager)
            : base(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IXmlSerializer>())
        {
            this.applicationHost = applicationHost;
            this.logger = logManager.GetLogger(this.Name);

            Instance = this;

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

            // Automatic cleanup via playback: no scheduled task, just react to
            // the user's play state changing.
            this.userDataManager.UserDataSaved += this.OnUserDataSaved;
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

        private void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            if (e?.User == null || e.Item == null || e.UserData == null || !e.UserData.Played)
            {
                return;
            }

            // Fire-and-forget: this is an event handler, it cannot be awaited.
            // Any failure here must not affect Emby's own playback/user-data
            // pipeline, so it's logged rather than allowed to throw.
            _ = Task.Run(async () =>
            {
                try
                {
                    var settings = await this.AdminSettingsStore.GetAsync().ConfigureAwait(false);
                    if (!settings.ClearWatchedRecommendations)
                    {
                        return;
                    }

                    await this.RecommendationService
                        .HandleItemWatchedAsync(e.User.InternalId, e.Item.InternalId, e.User)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("RecommendMe: error handling watched-item cleanup", ex);
                }
            });
        }
    }
}
