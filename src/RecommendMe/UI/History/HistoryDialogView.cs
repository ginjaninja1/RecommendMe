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
        private readonly IUserManager userManager;
        private readonly IJsonSerializer jsonSerializer;

        public HistoryDialogView(string pluginId, User viewer, IServerApplicationHost applicationHost)
            : base(pluginId)
        {
            this.viewer = viewer;
            this.userManager = applicationHost.Resolve<IUserManager>();
            this.jsonSerializer = applicationHost.Resolve<IJsonSerializer>();

            var ui = new HistoryUI
            {
                Grid = HistoryViewBuilder.BuildEmptyGrid(),
                SenderChoices = new[] { HistoryFilters.Anyone }
                    .Concat(this.userManager.Users.Select(u => u.Name))
                    .ToList()
            };

            this.ContentData = ui;

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
            }

            return this;
        }

        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            // History is a read-only view; closing via OK needs no side effects.
            return Task.CompletedTask;
        }
    }
}
