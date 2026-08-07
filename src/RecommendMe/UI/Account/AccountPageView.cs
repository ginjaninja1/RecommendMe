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
                        this.ContentData = AccountViewBuilder.BuildAsync(user).GetAwaiter().GetResult();
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

            if (AccountCommands.TryParseBlock(commandId, out var blockedSenderUserId))
            {
                var prefs = Plugin.Instance.UserPreferenceStore.GetForUserAsync(currentUser.InternalId).GetAwaiter().GetResult();

                var senderPref = prefs.SenderPreferences.FirstOrDefault(p => p.SenderUserId == blockedSenderUserId);
                if (senderPref == null)
                {
                    senderPref = new SenderPreference { SenderUserId = blockedSenderUserId };
                    prefs.SenderPreferences.Add(senderPref);
                }

                senderPref.Blocked = !senderPref.Blocked;

                prefs.UserId = currentUser.InternalId;
                Plugin.Instance.UserPreferenceStore.SaveForUserAsync(prefs).GetAwaiter().GetResult();
            }
            else if (AccountCommands.TryParse(commandId, out var senderUserId, out var mediaType))
            {
                var prefs = Plugin.Instance.UserPreferenceStore.GetForUserAsync(currentUser.InternalId).GetAwaiter().GetResult();

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
                    this.ContentData = AccountViewBuilder.BuildAsync(currentUser).GetAwaiter().GetResult();
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
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
                Plugin.Instance.UserPreferenceStore.SaveForUserAsync(prefs).GetAwaiter().GetResult();
            }

            this.ContentData = AccountViewBuilder.BuildAsync(currentUser).GetAwaiter().GetResult();
            this.RaiseUIViewInfoChanged();

            return Task.FromResult<IPluginUIView>(this);
        }
    }
}
