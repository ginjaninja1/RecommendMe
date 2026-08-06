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
            // after construction, so start with an empty view-model and
            // build the real (user-specific) content the first time we have
            // a user - see RunCommand.
            this.ContentData = new AccountUI();
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

            if (AccountCommands.TryParse(commandId, out var senderUserId, out var mediaType))
            {
                var prefs = Plugin.Instance.UserPreferenceStore.GetForUserAsync(currentUser.InternalId).GetAwaiter().GetResult();

                var senderPref = prefs.SenderPreferences.FirstOrDefault(p => p.SenderUserId == senderUserId);
                if (senderPref == null)
                {
                    senderPref = new SenderPreference { SenderUserId = senderUserId };
                    prefs.SenderPreferences.Add(senderPref);
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
