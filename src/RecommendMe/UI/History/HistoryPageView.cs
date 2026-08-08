using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>User-facing History tab that opens the full-screen history view.</summary>
    internal class HistoryPageView : PluginPageView
    {
        public HistoryPageView(PluginInfo pluginInfo)
            : base(pluginInfo.Id)
        {
            this.ShowSave = false;
            this.ShowBack = false;
            this.ContentData = new HistoryPageUI();
        }

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
                        var isAdministrator = value.Policy?.IsAdministrator == true;
                        this.ContentData = !isAdministrator
                            && Plugin.Instance.PermissionService.IsAccessSuspendedAsync(user).GetAwaiter().GetResult()
                            ? (MediaBrowser.Model.GenericEdit.IEditableObject)new SuspendedUI()
                            : new HistoryPageUI();
                    }
                }
            }
        }

        private User CurrentUser =>
            this.User != null ? Plugin.Instance.UserManager.GetUserById(this.User.Id) : null;

        public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var currentUser = this.CurrentUser;
            if (currentUser == null)
            {
                return this;
            }

            var isAdministrator = this.User?.Policy?.IsAdministrator == true;
            if (!isAdministrator
                && await Plugin.Instance.PermissionService.IsAccessSuspendedAsync(currentUser).ConfigureAwait(false))
            {
                this.ContentData = new SuspendedUI();
                this.RaiseUIViewInfoChanged();
                return this;
            }

            if (commandId == HistoryCommands.Open)
            {
                return await HistoryDialogView
                    .CreateAsync(this.PluginId, currentUser, isAdministrator)
                    .ConfigureAwait(false);
            }

            return this;
        }
    }
}
