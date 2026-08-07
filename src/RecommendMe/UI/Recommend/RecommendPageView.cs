using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.Services;
using RecommendMe.UI.History;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Recommend
{
    /// <summary>
    /// The main user-facing "Search &amp; Recommend" page. Deliberately kept
    /// to construction + command handlers, mirroring the split used by the
    /// admin ConfigPageView: RecommendUI is the view-model, RecommendViewBuilder
    /// builds its dynamic parts, RecommendCommands owns command id parsing.
    /// </summary>
    internal class RecommendPageView : PluginPageView
    {
        private readonly IServerApplicationHost applicationHost;
        private readonly IJsonSerializer jsonSerializer;
        private readonly MediaSearchService searchService;
        private readonly ILogger logger;

        public RecommendPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.applicationHost = applicationHost;
            this.logger = logger;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();
            this.searchService = new MediaSearchService(Plugin.Instance.LibraryManager, this.logger);

            this.ShowSave = false;
            this.ShowBack = false;

            // NOTE: this.User (the browsing user) is only populated by the
            // Emby UI-page framework AFTER this constructor returns (see
            // PageControllerHostBase.GetUIView: CreateDefaultPageView() runs
            // first, `currentUIView.User = userDto` runs after). So nothing
            // user-specific (target list, search results) can be built here -
            // it's built lazily in RunCommand below, where this.User is
            // guaranteed to already be set from the page's most recent load.
            this.ContentData = new RecommendUI();
        }

        /// <summary>
        /// Populates the target-user dropdown the moment the framework
        /// assigns the real browsing user (see PageControllerHostBase.GetUIView),
        /// rather than leaving it empty until the first Search/postback.
        /// </summary>
        public override MediaBrowser.Model.Dto.UserDto User
        {
            get => base.User;
            set
            {
                base.User = value;

                if (value != null)
                {
                    var user = Plugin.Instance.UserManager.GetUserById(value.Id);
                    if (user != null)
                    {
                        var ui = (RecommendUI)this.ContentData;
                        ui.TargetUserChoices = RecommendViewBuilder.BuildTargetUserChoicesAsync(user).GetAwaiter().GetResult();
                    }
                }
            }
        }

        /// <summary>Resolves the browsing user from the framework-assigned UserDto. Only valid inside RunCommand.</summary>
        private User CurrentUser =>
            this.User != null ? Plugin.Instance.UserManager.GetUserById(this.User.Id) : null;

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var ui = (RecommendUI)this.ContentData;

            // Mirrors the established pattern in this codebase (see the admin
            // ConfigPageView's HandleSave): explicitly deserialize the posted
            // payload rather than relying on the framework's own ContentData
            // reassignment, and copy across only the real (non-generated)
            // fields - SearchResults/TargetUserChoices/StatusMessage are
            // always server-rebuilt, never trusted from the client.
            if (!string.IsNullOrEmpty(data))
            {
                var incoming = this.jsonSerializer.DeserializeFromString<RecommendUI>(data);
                if (incoming != null)
                {
                    ui.SearchTerm = incoming.SearchTerm;
                    ui.SelectedTargetUserName = incoming.SelectedTargetUserName;
                    ui.IsPrivate = incoming.IsPrivate;
                }
            }

            var currentUser = this.CurrentUser;

            if (currentUser == null)
            {
                ui.StatusMessage = RecommendViewBuilder.BuildStatusMessage("Could not identify the current user - please reload the page.", false);
                return Task.FromResult<IPluginUIView>(this);
            }

            if (commandId == RecommendCommands.OpenHistory)
            {
                IPluginUIView dialog = new HistoryDialogView(this.PluginId, currentUser, this.applicationHost);
                return Task.FromResult(dialog);
            }

            if (commandId == RecommendCommands.Search)
            {
                return Task.FromResult(this.HandleSearch(ui, currentUser));
            }

            if (RecommendCommands.TryParseSend(commandId, out var itemToRecommendId))
            {
                return this.HandleSendAsync(ui, currentUser, itemToRecommendId);
            }

            if (RecommendCommands.TryParseExpand(commandId, out var itemToExpandId))
            {
                var parent = Plugin.Instance.LibraryManager.GetItemById(itemToExpandId);
                if (parent != null)
                {
                    RecommendViewBuilder.TryToggleChildren(
                        ui.SearchResults,
                        itemToExpandId,
                        () => this.searchService.GetChildren(currentUser, parent));
                }
                return Task.FromResult<IPluginUIView>(this);
            }

            // updateformstate and anything else: refresh the target list
            // (cheap) and re-render with whatever the client posted back.
            ui.TargetUserChoices = RecommendViewBuilder.BuildTargetUserChoicesAsync(currentUser).GetAwaiter().GetResult();
            return Task.FromResult<IPluginUIView>(this);
        }

        private IPluginUIView HandleSearch(RecommendUI ui, User currentUser)
        {
            try
            {
                ui.TargetUserChoices = RecommendViewBuilder.BuildTargetUserChoicesAsync(currentUser).GetAwaiter().GetResult();
                var allowedTypes = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().GloballyAllowedMediaTypes;
                var results = this.searchService.Search(currentUser, ui.SearchTerm, allowedTypes);
                ui.SearchResults = RecommendViewBuilder.BuildSearchResults(results);
                ui.StatusMessage = new Emby.Web.GenericEdit.Elements.List.GenericItemList();
            }
            catch (Exception ex)
            {
                this.logger.ErrorException(
                    $"Media search failed for user {currentUser.Name} ({currentUser.InternalId}), term '{LogValue(ui.SearchTerm)}'",
                    ex);
                ui.SearchResults = new Emby.Web.GenericEdit.Elements.List.GenericItemList();
                ui.StatusMessage = RecommendViewBuilder.BuildStatusMessage("Search failed. Check the Emby server log for RecommendMe diagnostics.", false);
            }

            return this;
        }

        private static string LogValue(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", "\\r").Replace("\n", "\\n");

        private async Task<IPluginUIView> HandleSendAsync(RecommendUI ui, User currentUser, long itemId)
        {
            try
            {
                var targetUser = RecommendViewBuilder.ResolveTargetUser(ui.SelectedTargetUserName, currentUser);
                var item = Plugin.Instance.LibraryManager.GetItemById(itemId);

                if (targetUser == null || item == null)
                {
                    RecommendViewBuilder.TrySetRecommendationStatus(ui.SearchResults, itemId, "Select a recipient before recommending.", false);
                    ui.StatusMessage = item == null
                        ? RecommendViewBuilder.BuildStatusMessage("That media item is no longer available.", false)
                        : new Emby.Web.GenericEdit.Elements.List.GenericItemList();
                    return this;
                }

                var mediaType = item.GetType().Name;

                var result = await Plugin.Instance.RecommendationService
                    .SendRecommendationAsync(currentUser, targetUser, item, mediaType, ui.IsPrivate)
                    .ConfigureAwait(false);

                var status = result switch
                {
                    RecommendationResult.Success => ($"Recommended to {targetUser.Name}.", true),
                    RecommendationResult.NotPermitted => ("You don't have permission to recommend this to that user.", false),
                    RecommendationResult.RecipientBlockedSender => ($"{targetUser.Name} is not accepting recommendations from you.", false),
                    RecommendationResult.RecipientOptedOutMediaType => ($"{targetUser.Name} is not accepting {mediaType} recommendations from you.", false),
                    RecommendationResult.AlreadyWatchedByRecipient => ($"{targetUser.Name} has already watched this.", false),
                    RecommendationResult.AlreadyInRecipientCollection => ($"{targetUser.Name} already has this in their recommendation collection.", false),
                    _ => ("Something went wrong.", false)
                };
                RecommendViewBuilder.TrySetRecommendationStatus(ui.SearchResults, itemId, status.Item1, status.Item2);
                ui.StatusMessage = new Emby.Web.GenericEdit.Elements.List.GenericItemList();
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error sending recommendation", ex);
                RecommendViewBuilder.TrySetRecommendationStatus(ui.SearchResults, itemId, "Something went wrong sending that recommendation.", false);
                ui.StatusMessage = new Emby.Web.GenericEdit.Elements.List.GenericItemList();
            }

            return this;
        }
    }
}
