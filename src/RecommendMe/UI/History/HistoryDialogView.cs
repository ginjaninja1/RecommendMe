using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>
    /// Recommendation History dialog. Loads every record the viewer is
    /// allowed to see (server-side privacy/visibility isolation - see
    /// HistoryViewBuilder.BuildRowsAsync) once on open. Date/sender/
    /// recipient/media-type narrowing is handled by DxDataGrid's filter row.
    /// </summary>
    internal class HistoryDialogView : PluginDialogView
    {
        private HistoryDialogView(string pluginId)
            : base(pluginId)
        {
            var ui = new HistoryUI();
            this.ContentData = ui;

            // Full-screen, matching the pattern used for equivalent dialogs
            // in ListProtection (RepairDialogView / GroundTruthDialogView).
            this.ShowDialogFullScreen = true;

            ui.Grid = HistoryViewBuilder.BuildEmptyGrid();
        }

        public static async Task<HistoryDialogView> CreateAsync(
            string pluginId,
            User viewer,
            bool isAdministrator)
        {
            var view = new HistoryDialogView(pluginId);
            ((HistoryUI)view.ContentData).Rows = await HistoryViewBuilder
                .BuildRowsAsync(viewer, isAdministrator)
                .ConfigureAwait(false);
            return view;
        }

        public override bool ShowDialogFullScreen { get; }

        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            // History is a read-only view; closing via OK needs no side effects.
            return Task.CompletedTask;
        }
    }
}
