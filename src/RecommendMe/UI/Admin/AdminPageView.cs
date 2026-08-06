using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    /// <summary>
    /// Admin permission-matrix page. Every toggle is its own instantly-saved
    /// command (mirroring the library/path toggles on the original template's
    /// ConfigPageView) rather than a batch "Save" - so admins see the effect
    /// of each change immediately and there is no unsaved-changes state to lose.
    /// </summary>
    internal class AdminPageView : PluginPageView
    {
        private readonly IUserManager userManager;
        private readonly ILogger logger;

        public AdminPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.userManager = applicationHost.Resolve<IUserManager>();
            this.logger = logger;

            this.ShowSave = false;
            this.ShowBack = false;

            this.RebuildContentData();
        }

        private void RebuildContentData()
        {
            foreach (var user in this.userManager.Users)
            {
                Plugin.Instance.PermissionService.EnsureUserAccessEntryAsync(user).GetAwaiter().GetResult();
            }

            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();

            this.ContentData = AdminViewBuilder.Build(settings, this.userManager.Users.ToList());
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var mutated = true;

            if (AdminCommands.IsSendScopeModeToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    s.SendScope = s.SendScope == Models.AccessScope.AllUsers ? Models.AccessScope.SpecificUsers : Models.AccessScope.AllUsers)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.IsReceiveScopeModeToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    s.ReceiveScope = s.ReceiveScope == Models.AccessScope.AllUsers ? Models.AccessScope.SpecificUsers : Models.AccessScope.AllUsers)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseSendScopeUser(commandId, out var sendScopeUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => ToggleListMembership(s.SendScopeUserIds, sendScopeUserId))
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseReceiveScopeUser(commandId, out var receiveScopeUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => ToggleListMembership(s.ReceiveScopeUserIds, receiveScopeUserId))
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseMediaType(commandId, out var mediaType))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => ToggleListMembership(s.GloballyAllowedMediaTypes, mediaType))
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.IsDefaultSendingToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.DefaultProfile.AllowSending = !s.DefaultProfile.AllowSending)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.IsDefaultReceivingToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.DefaultProfile.AllowReceiving = !s.DefaultProfile.AllowReceiving)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseDefaultMediaType(commandId, out var defaultMediaType))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => ToggleListMembership(s.DefaultProfile.AllowedMediaTypes, defaultMediaType))
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserSending(commandId, out var sendingUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == sendingUserId);
                    if (entry != null)
                    {
                        entry.AllowSending = !entry.AllowSending;
                    }
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserReceiving(commandId, out var receivingUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == receivingUserId);
                    if (entry != null)
                    {
                        entry.AllowReceiving = !entry.AllowReceiving;
                    }
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserMediaType(commandId, out var userId, out var userMediaType))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == userId);
                    if (entry != null)
                    {
                        ToggleListMembership(entry.AllowedMediaTypes, userMediaType);
                    }
                }).GetAwaiter().GetResult();
            }
            else
            {
                mutated = false;
            }

            if (mutated)
            {
                this.logger.Info("RecommendMe: admin settings updated (command '{0}')", commandId);
            }

            this.RebuildContentData();
            this.RaiseUIViewInfoChanged();

            return Task.FromResult<IPluginUIView>(this);
        }

        private static void ToggleListMembership<T>(System.Collections.Generic.List<T> list, T value)
        {
            if (list.Contains(value))
            {
                list.Remove(value);
            }
            else
            {
                list.Add(value);
            }
        }
    }
}
