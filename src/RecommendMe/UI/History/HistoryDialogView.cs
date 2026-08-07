using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
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
        private readonly IJsonSerializer jsonSerializer;

        public HistoryDialogView(string pluginId, User viewer, IServerApplicationHost applicationHost)
            : base(pluginId)
        {
            this.viewer = viewer;
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();

            var ui = new HistoryUI
            {
                Grid = HistoryViewBuilder.BuildEmptyGrid(),
                SenderChoices = HistoryUI.ToOptions(
                    new[] { HistoryFilters.Anyone }.Concat(Plugin.Instance.GetAllUsers().Select(u => u.Name)))
            };

            this.ContentData = ui;

            // Full-screen, matching the pattern used for equivalent dialogs
            // in ListProtection (RepairDialogView / GroundTruthDialogView):
            // set once here, exposed via the get-only override below.
            this.ShowDialogFullScreen = true;

            // Populate the default filtered view up front, mirroring how
            // ConfigPageView eagerly builds its ContentData in its own
            // constructor - this is a local-disk JSON read, not a network
            // call, so the synchronous wait here is bounded and acceptable.
            var rows = HistoryViewBuilder
                .BuildRowsAsync(this.viewer, HistoryFilters.Last3Months, HistoryFilters.CurrentUser, HistoryFilters.Anyone)
                .GetAwaiter()
                .GetResult();

            ui.Grid.Options.dataSource = rows;
        }

        public override bool ShowDialogFullScreen { get; }

        public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (commandId == HistoryCommands.Refresh && !string.IsNullOrEmpty(data))
            {
                var incoming = this.jsonSerializer.DeserializeFromString<HistoryUI>(data);
                var ui = (HistoryUI)this.ContentData;

                if (incoming != null)
                {
                    ui.SelectedDateRange = incoming.SelectedDateRange;
                    ui.SelectedRecipient = incoming.SelectedRecipient;
                    ui.SelectedSender = incoming.SelectedSender;
                }

                var rows = await HistoryViewBuilder
                    .BuildRowsAsync(this.viewer, ui.SelectedDateRange, ui.SelectedRecipient, ui.SelectedSender)
                    .ConfigureAwait(false);
                ui.Grid.Options.dataSource = rows;

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

        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            // History is a read-only view; closing via OK needs no side effects.
            return Task.CompletedTask;
        }
    }
}