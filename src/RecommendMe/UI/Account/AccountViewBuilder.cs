using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
using RecommendMe.Services;

namespace RecommendMe.UI.Account
{
    internal static class AccountViewBuilder
    {
        /// <summary>
        /// Builds the sender list for <paramref name="viewer"/>: every other
        /// user whose SendMode currently permits sending to this viewer, each
        /// with an opt-out toggle per server-wide media type. This narrows the
        /// admin-configured access model - it never grants anything the admin
        /// hasn't already allowed, matching PermissionService's rule.
        /// </summary>
        public static async Task<AccountUI> BuildAsync(User viewer)
        {
            var plugin = Plugin.Instance;
            var settings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            var preferences = await plugin.UserPreferenceStore.GetForUserAsync(viewer.InternalId).ConfigureAwait(false);

            var viewerEntry = await plugin.PermissionService.EnsureUserAccessEntryAsync(viewer).ConfigureAwait(false);

            var senderList = new GenericItemList();

            if (!viewerEntry.AccessSuspended)
            {
                foreach (var candidate in plugin.GetAllUsers().Where(u => u.InternalId != viewer.InternalId).OrderBy(u => u.Name))
                {
                    var candidateEntry = await plugin.PermissionService.EnsureUserAccessEntryAsync(candidate).ConfigureAwait(false);
                    if (candidateEntry.AccessSuspended || !PermissionService.IsTargetAllowed(candidateEntry, viewer.InternalId))
                    {
                        continue;
                    }

                    var existingPref = preferences.SenderPreferences.FirstOrDefault(p => p.SenderUserId == candidate.InternalId);
                    var optedOut = existingPref?.OptedOutMediaTypes ?? new System.Collections.Generic.List<string>();
                    var isBlocked = existingPref?.Blocked ?? false;

                    var subItems = new GenericItemList();
                    foreach (var mediaType in settings.GloballyAllowedMediaTypes)
                    {
                        var isOptedIn = !optedOut.Contains(mediaType);
                        subItems.Add(new GenericListItem
                        {
                            PrimaryText = $"  {mediaType}",
                            Status = isOptedIn ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                            Toggle = new ToggleButtonItem("Receive")
                            {
                                IsChecked = isOptedIn && !isBlocked,
                                IsEnabled = !isBlocked,
                                CommandId = AccountCommands.BuildOptOutToggle(candidate.InternalId, mediaType)
                            }
                        });
                    }

                    senderList.Add(new GenericListItem
                    {
                        PrimaryText = candidate.Name,
                        Status = isBlocked ? ItemStatus.Failed : ItemStatus.Succeeded,
                        Toggle = new ToggleButtonItem("Accept recommendations")
                        {
                            IsChecked = !isBlocked,
                            CommandId = AccountCommands.BuildBlockToggle(candidate.InternalId)
                        },
                        SubItems = subItems
                    });
                }
            }

            return new AccountUI { SenderList = senderList };
        }
    }
}