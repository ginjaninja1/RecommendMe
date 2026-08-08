using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>User-facing History tab that opens the full-screen history view.</summary>
    internal class HistoryPageView : PluginPageView
    {
        private readonly IServerApplicationHost applicationHost;

        public HistoryPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost)
            : base(pluginInfo.Id)
        {
            this.applicationHost = applicationHost;
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
                        this.ContentData = Plugin.Instance.PermissionService.IsAccessSuspendedAsync(user).GetAwaiter().GetResult()
                            ? (MediaBrowser.Model.GenericEdit.IEditableObject)new SuspendedUI()
                            : new HistoryPageUI();
                    }
                }
            }
        }

        private User CurrentUser =>
            this.User != null ? Plugin.Instance.UserManager.GetUserById(this.User.Id) : null;

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var currentUser = this.CurrentUser;
            if (currentUser == null)
            {
                return Task.FromResult<IPluginUIView>(this);
            }

            if (Plugin.Instance.PermissionService.IsAccessSuspendedAsync(currentUser).GetAwaiter().GetResult())
            {
                this.ContentData = new SuspendedUI();
                this.RaiseUIViewInfoChanged();
                return Task.FromResult<IPluginUIView>(this);
            }

            if (commandId == HistoryCommands.Open)
            {
                IPluginUIView dialog = new HistoryDialogView(this.PluginId, currentUser, this.applicationHost);
                return Task.FromResult(dialog);
            }

            return Task.FromResult<IPluginUIView>(this);
        }
    }
}
