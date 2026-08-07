using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.Models;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    /// <summary>
    /// Admin permissions page. Every toggle/button is its own instantly-saved
    /// command (mirroring the library/path toggles on the original template's
    /// ConfigPageView) rather than a batch "Save" - so admins see the effect
    /// of each change immediately and there is no unsaved-changes state to lose.
    /// </summary>
    internal class AdminPageView : PluginPageView
    {
        private readonly ILogger logger;

        public AdminPageView(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;

            this.ShowSave = false;
            this.ShowBack = false;

            this.RebuildContentData();
        }

        private void RebuildContentData()
        {
            var allUsers = Plugin.Instance.GetAllUsers();

            foreach (var user in allUsers)
            {
                Plugin.Instance.PermissionService.EnsureUserAccessEntryAsync(user).GetAwaiter().GetResult();
            }

            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();

            this.ContentData = AdminViewBuilder.Build(settings, allUsers);
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var mutated = true;

            if (AdminCommands.TryParseMediaType(commandId, out var mediaType))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => ToggleListMembership(s.GloballyAllowedMediaTypes, mediaType))
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.IsNewUserDefaultSendModeToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    s.NewUserDefaultSendMode = s.NewUserDefaultSendMode == SendMode.Everyone ? SendMode.NoOne : SendMode.Everyone)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.IsAutoGrantToggle(commandId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.AutoGrantNewUsersToExistingSendLists = !s.AutoGrantNewUsersToExistingSendLists)
                    .GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserSuspended(commandId, out var suspendedUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == suspendedUserId);
                    if (entry != null)
                    {
                        entry.AccessSuspended = !entry.AccessSuspended;
                    }
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserSendMode(commandId, out var sendModeUserId, out var sendMode))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == sendModeUserId);
                    if (entry != null)
                    {
                        entry.SendMode = sendMode;
                    }
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryParseUserTarget(commandId, out var userId, out var targetUserId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var entry = s.UserAccess.FirstOrDefault(u => u.UserId == userId);
                    if (entry != null)
                    {
                        ToggleListMembership(entry.AllowedTargetUserIds, targetUserId);
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