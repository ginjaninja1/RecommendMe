using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>
    /// Recommendation History dialog. Loads every record the viewer is
    /// allowed to see (server-side privacy/visibility isolation - see
    /// HistoryViewBuilder.BuildRowsAsync) once on open. Date/sender/
    /// recipient/media-type narrowing is left entirely to DxDataGrid's own
    /// filter row on the client - there is no server-side filter dropdown or
    /// refresh postback here anymore (removed 2026-08-07; see
    /// HistoryUI/HistoryViewBuilder remarks for why).
    /// </summary>
    internal class HistoryDialogView : PluginDialogView
    {
        public HistoryDialogView(
            string pluginId,
            User viewer,
            bool isAdministrator,
            IServerApplicationHost applicationHost)
            : base(pluginId)
        {
            var ui = new HistoryUI();
            this.ContentData = ui;

            // Full-screen, matching the pattern used for equivalent dialogs
            // in ListProtection (RepairDialogView / GroundTruthDialogView).
            this.ShowDialogFullScreen = true;

            ui.Grid = HistoryViewBuilder.BuildEmptyGrid();
            var rows = HistoryViewBuilder.BuildRowsAsync(viewer, isAdministrator).GetAwaiter().GetResult();
            ui.Rows = rows;
        }

        public override bool ShowDialogFullScreen { get; }

        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            // History is a read-only view; closing via OK needs no side effects.
            return Task.CompletedTask;
        }
    }
}
