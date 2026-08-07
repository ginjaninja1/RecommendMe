using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    internal class AdminPageView : PluginPageView
    {
        private readonly ILogger logger;
        private readonly IServerApplicationHost applicationHost;
        private readonly IJsonSerializer serializer;

        public AdminPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.applicationHost = applicationHost;
            this.serializer = applicationHost.Resolve<IJsonSerializer>();
            this.ShowSave = false;
            this.ShowBack = false;
            this.Rebuild(new AdminSettingsUI());
        }

        private void Rebuild(AdminSettingsUI state)
        {
            var users = Plugin.Instance.GetAllUsers();
            foreach (var user in users)
            {
                Plugin.Instance.PermissionService.EnsureUserAccessEntryAsync(user).GetAwaiter().GetResult();
            }

            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            this.ContentData = AdminViewBuilder.Build(settings, users, state);
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var state = string.IsNullOrEmpty(data)
                ? (AdminSettingsUI)this.ContentData
                : this.serializer.DeserializeFromString<AdminSettingsUI>(data) ?? new AdminSettingsUI();

            if (commandId == AdminCommands.ToggleExpansion)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.AlwaysExpandUsersAndGroups = state.AlwaysExpandUsersAndGroups).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TrySuspended(commandId, out var userId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(e => e.UserId == userId);
                    if (entry != null) entry.AccessSuspended = !entry.AccessSuspended;
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TrySendTo(commandId, out userId))
            {
                return Task.FromResult<IPluginUIView>(new SendToDialogView(this.PluginId, userId, this, () => this.Rebuild(state), this.applicationHost, this.logger));
            }
            else if (AdminCommands.TryReceiveFrom(commandId, out userId))
            {
                return Task.FromResult<IPluginUIView>(new ReceiveFromDialogView(this.PluginId, userId, this, () => this.Rebuild(state), this.applicationHost, this.logger));
            }
            else if (AdminCommands.TryMembership(commandId, out userId))
            {
                return Task.FromResult<IPluginUIView>(new UserGroupMembershipDialogView(this.PluginId, userId, this, () => this.Rebuild(state), this.applicationHost, this.logger));
            }
            else if (commandId == AdminCommands.DefaultPolicyRefresh)
            {
                return Task.FromResult<IPluginUIView>(new DefaultUserPolicyDialogView(this.PluginId, this, () => this.Rebuild(state), this.applicationHost, this.logger));
            }

            this.logger.Info("RecommendMe: admin users command '{0}'", commandId ?? "(null)");
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }

        public override void OnDialogResult(IPluginUIView dialogView, bool completedOk, object data)
        {
            this.Rebuild((AdminSettingsUI)this.ContentData);
            this.RaiseUIViewInfoChanged();
        }
    }
}
