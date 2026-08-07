using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>
    /// Recommendation History dialog (spec section 4). Default filters on
    /// open: Last 3 Months / Recommended To = Me / From = Anyone.
    /// </summary>
    internal class HistoryDialogView : PluginDialogView
    {
        private readonly User viewer;
        private readonly List<Emby.Web.GenericEdit.Common.EditorSelectOption> senderChoices;

        public HistoryDialogView(string pluginId, User viewer, IServerApplicationHost applicationHost)
            : base(pluginId)
        {
            this.viewer = viewer;

            // "Me" substitution for the viewer's own name, matching the
            // Recommend page's target picker. Value stays the raw username
            // (HistoryViewBuilder.BuildRowsAsync compares senderFilter
            // against SentByUserName as-is) - only the display label changes.
            this.senderChoices = new List<Emby.Web.GenericEdit.Common.EditorSelectOption>
            {
                new Emby.Web.GenericEdit.Common.EditorSelectOption(HistoryFilters.Anyone, HistoryFilters.Anyone)
            };
            foreach (var name in Plugin.Instance.GetAllUsers().Select(u => u.Name))
            {
                var label = name == viewer.Name ? name + " (me)" : name;
                this.senderChoices.Add(new Emby.Web.GenericEdit.Common.EditorSelectOption(name, label));
            }

            var ui = new HistoryUI { SenderChoices = this.senderChoices };
            this.ContentData = ui;

            // Full-screen, matching the pattern used for equivalent dialogs
            // in ListProtection (RepairDialogView / GroundTruthDialogView):
            // set once here, exposed via the get-only override below.
            this.ShowDialogFullScreen = true;

            this.RebuildGrid(ui, HistoryFilters.Last3Months, HistoryFilters.CurrentUser, HistoryFilters.Anyone)
                .GetAwaiter()
                .GetResult();
        }

        public override bool ShowDialogFullScreen { get; }

        public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (commandId == HistoryCommands.Refresh && !string.IsNullOrEmpty(data))
            {
                // NOTE: PageControllerHostBase.RunCommand (Emby core) has
                // already replaced this.ContentData with a new HistoryUI
                // built purely from the client's posted JSON, before this
                // method runs - see PageControllerHostBase.RunCommand's
                // DeserializeFromJsonString call. That round-trip is not
                // guaranteed to carry a fully-formed Grid or choice lists
                // back with it, so treat this.ContentData as
                // untrustworthy for anything server-authoritative: read
                // only the three real filter fields off it, then rebuild
                // everything else fresh. Trusting ui.Grid here (as the
                // previous version did) is what caused the NRE on
                // ui.Grid.Options.dataSource.
                var ui = (HistoryUI)this.ContentData;
                ui.SenderChoices = this.senderChoices;

                await this.RebuildGrid(ui, ui.SelectedDateRange, ui.SelectedRecipient, ui.SelectedSender).ConfigureAwait(false);

                return this;
            }

            // Anything else - "DialogCancel", "DialogOk", or unknown - must
            // fall through to DialogViewBase (see base.RunCommand) rather
            // than unconditionally returning `this`. DialogViewBase is the
            // only thing that actually returns the parent view; blanket-
            // returning `this` kept the dialog "open" against stale
            // ContentData on every Cancel/Save click, which is what broke
            // navigation and threw the client-side keyExpr error.
            return await base.RunCommand(itemId, commandId, data).ConfigureAwait(false);
        }

        private async Task RebuildGrid(HistoryUI ui, string dateRangeFilter, string recipientFilter, string senderFilter)
        {
            ui.Grid = HistoryViewBuilder.BuildEmptyGrid();
            var rows = await HistoryViewBuilder
                .BuildRowsAsync(this.viewer, dateRangeFilter, recipientFilter, senderFilter)
                .ConfigureAwait(false);
            ui.Grid.Options.dataSource = rows;
        }

        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            // History is a read-only view; closing via OK needs no side effects.
            return Task.CompletedTask;
        }
    }
}