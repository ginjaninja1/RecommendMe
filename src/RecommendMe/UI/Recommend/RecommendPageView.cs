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
        private readonly IJsonSerializer jsonSerializer;
        private readonly MediaSearchService searchService;
        private readonly ILogger logger;

        public RecommendPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
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
                        if (Plugin.Instance.PermissionService.IsAccessSuspendedAsync(user).GetAwaiter().GetResult())
                        {
                            this.ContentData = new SuspendedUI();
                            return;
                        }

                        var ui = (RecommendUI)this.ContentData;
                        ui.TargetUserChoices = RecommendViewBuilder.BuildTargetUserChoicesAsync(user).GetAwaiter().GetResult();
                        var allowedTypes = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().GloballyAllowedMediaTypes;
                        ui.MediaTypeChoices = RecommendViewBuilder.BuildMediaTypeChoices(allowedTypes);
                        ui.SelectedMediaTypes = string.Join(",", allowedTypes);
                    }
                }
            }
        }

        /// <summary>Resolves the browsing user from the framework-assigned UserDto. Only valid inside RunCommand.</summary>
        private User CurrentUser =>
            this.User != null ? Plugin.Instance.UserManager.GetUserById(this.User.Id) : null;

        public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var currentUser = this.CurrentUser;

            if (currentUser == null)
            {
                var unavailableUi = this.ContentData as RecommendUI ?? new RecommendUI();
                unavailableUi.StatusMessage = RecommendViewBuilder.BuildStatusMessage("Could not identify the current user - please reload the page.", false);
                this.ContentData = unavailableUi;
                return this;
            }

            // Check every postback as well as initial page rendering. This
            // prevents stale or forged commands from searching, expanding
            // results, or attempting a recommendation.
            if (await Plugin.Instance.PermissionService.IsAccessSuspendedAsync(currentUser).ConfigureAwait(false))
            {
                this.ContentData = new SuspendedUI();
                this.RaiseUIViewInfoChanged();
                return this;
            }

            var ui = this.ContentData as RecommendUI ?? new RecommendUI();
            this.ContentData = ui;

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
                    ui.SelectedMediaTypes = incoming.SelectedMediaTypes ?? string.Empty;
                    ui.SelectedTargetUserId = incoming.SelectedTargetUserId;
                    ui.IsPrivate = incoming.IsPrivate;
                }
            }

            if (commandId == RecommendCommands.Search)
            {
                return await this.HandleSearchAsync(ui, currentUser).ConfigureAwait(false);
            }

            if (RecommendCommands.TryParseSend(commandId, out var itemToRecommendId))
            {
                return await this.HandleSendAsync(ui, currentUser, itemToRecommendId).ConfigureAwait(false);
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
                return this;
            }

            // updateformstate and anything else: refresh the target list
            // (cheap) and re-render with whatever the client posted back.
            ui.TargetUserChoices = await RecommendViewBuilder.BuildTargetUserChoicesAsync(currentUser).ConfigureAwait(false);
            return this;
        }

        private async Task<IPluginUIView> HandleSearchAsync(RecommendUI ui, User currentUser)
        {
            try
            {
                ui.TargetUserChoices = await RecommendViewBuilder.BuildTargetUserChoicesAsync(currentUser).ConfigureAwait(false);
                var globallyAllowedTypes = (await Plugin.Instance.AdminSettingsStore.GetAsync().ConfigureAwait(false)).GloballyAllowedMediaTypes;
                ui.MediaTypeChoices = RecommendViewBuilder.BuildMediaTypeChoices(globallyAllowedTypes);
                var selectedTypes = (ui.SelectedMediaTypes ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(type => type.Trim())
                    .Where(type => globallyAllowedTypes.Contains(type))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                ui.SelectedMediaTypes = string.Join(",", selectedTypes);
                var results = this.searchService.Search(currentUser, ui.SearchTerm, selectedTypes);
                ui.SearchResults = RecommendViewBuilder.BuildSearchResults(results);
                RecommendViewBuilder.SetSearchActionMessage(ui, results.Count);
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
                var targetUser = RecommendViewBuilder.ResolveTargetUser(ui.SelectedTargetUserId, currentUser);
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
                    RecommendationSendResult.Success => ($"Recommended to {targetUser.Name}.", true),
                    RecommendationSendResult.NotPermitted => ("You don't have permission to recommend this to that user.", false),
                    RecommendationSendResult.RecipientBlockedSender => ($"{targetUser.Name} is not accepting recommendations from you.", false),
                    RecommendationSendResult.RecipientOptedOutMediaType => ($"{targetUser.Name} is not accepting {mediaType} recommendations from you.", false),
                    RecommendationSendResult.AlreadyWatchedByRecipient => ($"{targetUser.Name} has already watched this.", false),
                    RecommendationSendResult.AlreadyInRecipientCollection => ($"{targetUser.Name} already has this in their recommendation collection.", false),
                    RecommendationSendResult.RecipientCannotAccessItem => ($"{targetUser.Name} doesn't have access to this item.", false),
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