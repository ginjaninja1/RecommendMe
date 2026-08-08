using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.Models;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Account
{
    internal class AccountPageView : PluginPageView
    {
        private readonly ILogger logger;

        public AccountPageView(PluginInfo pluginInfo, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.ShowSave = false;
            this.ShowBack = false;

            // As with RecommendPageView: this.User isn't populated until
            // after construction. ContentData is fully rebuilt as soon as
            // it is, via the User override below - so this empty
            // view-model is only ever shown for the instant between
            // construction and that assignment.
            this.ContentData = new AccountUI();
        }

        /// <summary>
        /// Builds the real (user-specific) sender list the moment the
        /// framework assigns the browsing user - see
        /// PageControllerHostBase.GetUIView. This is what lets the Account
        /// tab show real content on first navigation, no "Load" click
        /// required.
        /// </summary>
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
                            : AccountViewBuilder.BuildAsync(user).GetAwaiter().GetResult();
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

            // The rendered controls are not the security boundary: reject
            // stale or forged preference commands before touching the store.
            if (await Plugin.Instance.PermissionService.IsAccessSuspendedAsync(currentUser).ConfigureAwait(false))
            {
                this.ContentData = new SuspendedUI();
                this.RaiseUIViewInfoChanged();
                return this;
            }

            if (AccountCommands.TryParseBlock(commandId, out var blockedSenderUserId))
            {
                var prefs = await Plugin.Instance.UserPreferenceStore.GetForUserAsync(currentUser.InternalId).ConfigureAwait(false);

                var senderPref = prefs.SenderPreferences.FirstOrDefault(p => p.SenderUserId == blockedSenderUserId);
                if (senderPref == null)
                {
                    senderPref = new SenderPreference { SenderUserId = blockedSenderUserId };
                    prefs.SenderPreferences.Add(senderPref);
                }

                senderPref.Blocked = !senderPref.Blocked;

                prefs.UserId = currentUser.InternalId;
                await Plugin.Instance.UserPreferenceStore.SaveForUserAsync(prefs).ConfigureAwait(false);
            }
            else if (AccountCommands.TryParse(commandId, out var senderUserId, out var mediaType))
            {
                var settings = await Plugin.Instance.AdminSettingsStore.GetAsync().ConfigureAwait(false);
                if (!settings.GloballyAllowedMediaTypes.Contains(mediaType))
                {
                    // Centrally disabled media types remain visible so users can
                    // understand the effective policy, but their saved choice is
                    // read-only until an administrator enables the type again.
                    this.ContentData = await AccountViewBuilder.BuildAsync(currentUser).ConfigureAwait(false);
                    this.RaiseUIViewInfoChanged();
                    return this;
                }

                var prefs = await Plugin.Instance.UserPreferenceStore.GetForUserAsync(currentUser.InternalId).ConfigureAwait(false);

                var senderPref = prefs.SenderPreferences.FirstOrDefault(p => p.SenderUserId == senderUserId);
                if (senderPref == null)
                {
                    senderPref = new SenderPreference { SenderUserId = senderUserId };
                    prefs.SenderPreferences.Add(senderPref);
                }

                // A disabled child toggle must also be read-only at the command
                // boundary; preserve the media choices while the sender's
                // master Accept recommendations switch is off.
                if (senderPref.Blocked)
                {
                    this.ContentData = await AccountViewBuilder.BuildAsync(currentUser).ConfigureAwait(false);
                    this.RaiseUIViewInfoChanged();
                    return this;
                }

                if (senderPref.OptedOutMediaTypes.Contains(mediaType))
                {
                    senderPref.OptedOutMediaTypes.Remove(mediaType);
                }
                else
                {
                    senderPref.OptedOutMediaTypes.Add(mediaType);
                }

                prefs.UserId = currentUser.InternalId;
                await Plugin.Instance.UserPreferenceStore.SaveForUserAsync(prefs).ConfigureAwait(false);
            }

            this.ContentData = await AccountViewBuilder.BuildAsync(currentUser).ConfigureAwait(false);
            this.RaiseUIViewInfoChanged();

            return this;
        }
    }
}
