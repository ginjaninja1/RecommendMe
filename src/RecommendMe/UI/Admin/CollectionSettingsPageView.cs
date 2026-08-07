using System;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    internal class CollectionSettingsPageView : PluginPageView
    {
        private readonly IJsonSerializer serializer;
        private readonly ILogger logger;

        public CollectionSettingsPageView(PluginInfo pluginInfo, IServerApplicationHost host, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.serializer = host.Resolve<IJsonSerializer>();
            this.logger = logger;
            this.ShowSave = false;
            this.ShowBack = false;
            this.Rebuild(null);
        }

        private void Rebuild(CollectionSettingsUI state)
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            this.ContentData = state ?? new CollectionSettingsUI
            {
                ClearWatchedRecommendations = settings.ClearWatchedRecommendations,
                PreventWatchedRecommendations = settings.PreventWatchedRecommendations,
                RecommendationCollectionPrefix = settings.RecommendationCollectionPrefix ?? string.Empty,
                RecommendationCollectionSuffix = settings.RecommendationCollectionSuffix ?? string.Empty
            };
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var state = string.IsNullOrEmpty(data)
                ? (CollectionSettingsUI)this.ContentData
                : this.serializer.DeserializeFromString<CollectionSettingsUI>(data) ?? new CollectionSettingsUI();

            if (commandId == CollectionSettingsCommands.SaveWatchedSettings)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(settings =>
                {
                    settings.ClearWatchedRecommendations = state.ClearWatchedRecommendations;
                    settings.PreventWatchedRecommendations = state.PreventWatchedRecommendations;
                }).GetAwaiter().GetResult();

                this.logger.Info(
                    "Watched recommendation settings saved; clear={0}, prevent={1}.",
                    state.ClearWatchedRecommendations,
                    state.PreventWatchedRecommendations);
            }
            else if (commandId == CollectionSettingsCommands.Apply)
            {
                state.RecommendationCollectionPrefix = state.RecommendationCollectionPrefix ?? string.Empty;
                state.RecommendationCollectionSuffix = state.RecommendationCollectionSuffix ?? string.Empty;

                Plugin.Instance.AdminSettingsStore.MutateAsync(settings =>
                {
                    settings.RecommendationCollectionPrefix = state.RecommendationCollectionPrefix;
                    settings.RecommendationCollectionSuffix = state.RecommendationCollectionSuffix;
                }).GetAwaiter().GetResult();

                try
                {
                    var result = Plugin.Instance.CollectionSyncService
                        .RenameInstantiatedCollectionsAsync(state.RecommendationCollectionPrefix, state.RecommendationCollectionSuffix)
                        .GetAwaiter().GetResult();
                    SetStatus(state, $"Collection naming saved. Renamed {result.Renamed} existing collection(s); skipped {result.Skipped} stale or ownerless registry entry/entries.", true);
                    this.logger.Info("Collection naming applied; renamed={0}, skipped={1}.", result.Renamed, result.Skipped);
                }
                catch (Exception ex)
                {
                    SetStatus(state, "Collection naming saved, but one or more existing collections could not be renamed. See the server log.", false);
                    this.logger.ErrorException("Failed while applying collection names", ex);
                }
            }

            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }

        private static void SetStatus(CollectionSettingsUI state, string message, bool success)
        {
            if (state.ApplyAction == null || state.ApplyAction.Count == 0)
            {
                state.ApplyAction = new CollectionSettingsUI().ApplyAction;
            }

            state.ApplyAction[0].SecondaryText = message;
            state.ApplyAction[0].Status = success ? ItemStatus.Succeeded : ItemStatus.Failed;
        }
    }
}
