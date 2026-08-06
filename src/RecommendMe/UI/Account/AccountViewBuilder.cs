using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;

namespace RecommendMe.UI.Account
{
    internal static class AccountViewBuilder
    {
        /// <summary>
        /// Builds the sender list for <paramref name="viewer"/>: every other
        /// user the admin's global scope currently permits as a sender to
        /// this viewer, each with an opt-out toggle per media type. This
        /// narrows the admin matrix - it never grants anything the admin
        /// hasn't already allowed, matching PermissionService's rule.
        /// </summary>
        public static async Task<AccountUI> BuildAsync(User viewer)
        {
            var plugin = Plugin.Instance;
            var settings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            var preferences = await plugin.UserPreferenceStore.GetForUserAsync(viewer.InternalId).ConfigureAwait(false);

            var viewerEntry = settings.UserAccess.FirstOrDefault(u => u.UserId == viewer.InternalId);
            var viewerAllowedTypes = viewerEntry?.AllowedMediaTypes ?? RecommendableMediaTypes.All.ToList();

            var senderList = new GenericItemList();

            var receiveOk = settings.ReceiveScope == AccessScope.AllUsers
                || settings.ReceiveScopeUserIds.Contains(viewer.InternalId);

            if (receiveOk && (viewerEntry?.AllowReceiving ?? true))
            {
                foreach (var candidate in plugin.UserManager.Users.Where(u => u.InternalId != viewer.InternalId).OrderBy(u => u.Name))
                {
                    var sendOk = settings.SendScope == AccessScope.AllUsers
                        || settings.SendScopeUserIds.Contains(candidate.InternalId);

                    var candidateEntry = settings.UserAccess.FirstOrDefault(u => u.UserId == candidate.InternalId);
                    if (!sendOk || candidateEntry == null || !candidateEntry.AllowSending)
                    {
                        continue;
                    }

                    var existingPref = preferences.SenderPreferences.FirstOrDefault(p => p.SenderUserId == candidate.InternalId);
                    var optedOut = existingPref?.OptedOutMediaTypes ?? new System.Collections.Generic.List<string>();

                    var subItems = new GenericItemList();
                    foreach (var mediaType in candidateEntry.AllowedMediaTypes.Intersect(viewerAllowedTypes))
                    {
                        var isOptedIn = !optedOut.Contains(mediaType);
                        subItems.Add(new GenericListItem
                        {
                            PrimaryText = $"  {mediaType}",
                            Status = isOptedIn ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                            Toggle = new ToggleButtonItem("Receive")
                            {
                                IsChecked = isOptedIn,
                                CommandId = AccountCommands.BuildOptOutToggle(candidate.InternalId, mediaType)
                            }
                        });
                    }

                    senderList.Add(new GenericListItem
                    {
                        PrimaryText = candidate.Name,
                        SubItems = subItems
                    });
                }
            }

            return new AccountUI { SenderList = senderList };
        }
    }
}
