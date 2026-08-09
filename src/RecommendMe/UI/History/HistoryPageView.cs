using System.Collections.Concurrent;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.History
{
    /// <summary>User-facing History tab that opens the full-screen history view.</summary>
    internal class HistoryPageView : PluginPageView
    {
        private readonly IJsonSerializer jsonSerializer;

        public HistoryPageView(PluginInfo pluginInfo, IJsonSerializer jsonSerializer)
            : base(pluginInfo.Id)
        {
            this.jsonSerializer = jsonSerializer;
            this.ShowSave = false;
            this.ShowBack = false;
            this.ContentData = new HistoryPageUI();
        }

        private readonly ConcurrentDictionary<string, bool> adminByUserId =
            new ConcurrentDictionary<string, bool>();

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

                        // This controller instance is shared server-wide (see
                        // RecommendUI.OwnerUserId), so admin status is tracked per user
                        // id here rather than trusted from this.User at RunCommand time.
                        this.adminByUserId[value.Id] = isAdministrator;

                        this.ContentData = !isAdministrator
                            && Plugin.Instance.PermissionService.IsAccessSuspendedAsync(user).GetAwaiter().GetResult()
                            ? (MediaBrowser.Model.GenericEdit.IEditableObject)new SuspendedUI()
                            : new HistoryPageUI { OwnerUserId = value.Id };
                    }
                }
            }
        }

        public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            // See RecommendUI.OwnerUserId - identity comes from the round-tripped
            // payload, not this.User, since this.User is only refreshed on GetUIView.
            var incoming = string.IsNullOrEmpty(data)
                ? null
                : this.jsonSerializer.DeserializeFromString<HistoryPageUI>(data);
            var ownerUserId = incoming?.OwnerUserId;
            var currentUser = string.IsNullOrEmpty(ownerUserId)
                ? null
                : Plugin.Instance.UserManager.GetUserById(ownerUserId);

            if (currentUser == null)
            {
                return this;
            }

            var isAdministrator = this.adminByUserId.TryGetValue(ownerUserId, out var admin) && admin;
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