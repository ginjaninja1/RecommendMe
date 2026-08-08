using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace RecommendMe.Tasks
{
    /// <summary>User-schedulable reconciliation; administrators choose the trigger in Emby.</summary>
    public class ClearWatchedRecommendationsTask : IScheduledTask
    {
        public string Name => "RecommendMe - Clear watched recommendations";
        public string Key => "RecommendMeClearWatchedRecommendations";
        public string Description => "Removes watched items from plugin-controlled recommendation collections when the corresponding admin setting is enabled.";
        public string Category => "GinjaNinja Tools";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                progress?.Report(100);
                return;
            }

            var settings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            if (!settings.ClearWatchedRecommendations)
            {
                plugin.Logger.Info("Watched-recommendation task exited because the setting is disabled.");
                progress?.Report(100);
                return;
            }

            var removed = await plugin.RecommendationService
                .ClearWatchedRecommendationsAsync(cancellationToken, progress)
                .ConfigureAwait(false);
            plugin.Logger.Info("Watched-recommendation task removed {0} item(s).", removed);
        }
    }
}
