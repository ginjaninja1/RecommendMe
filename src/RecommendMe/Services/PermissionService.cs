using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Resolves the effective send/receive/media-type permissions for a user,
    /// combining the admin-configured global scope, the per-user
    /// <see cref="UserAccessEntry"/> (materialized from <see cref="DefaultUserProfile"/>
    /// the first time a user is seen), and the user's own Account-tab
    /// opt-outs (<see cref="UserPreferenceStore"/>).
    ///
    /// Resolution order (most to least restrictive wins):
    /// 1. Global SendScope/ReceiveScope (AllUsers vs SpecificUsers allow-list)
    /// 2. Per-user AllowSending/AllowReceiving (covers Emergency Revocation)
    /// 3. Per-user AllowedMediaTypes ∩ GloballyAllowedMediaTypes
    /// 4. Recipient's own opt-out of this specific sender/media-type
    /// </summary>
    public class PermissionService
    {
        private readonly AdminSettingsStore adminSettingsStore;
        private readonly UserPreferenceStore userPreferenceStore;

        public PermissionService(AdminSettingsStore adminSettingsStore, UserPreferenceStore userPreferenceStore)
        {
            this.adminSettingsStore = adminSettingsStore;
            this.userPreferenceStore = userPreferenceStore;
        }

        /// <summary>
        /// Ensures a UserAccessEntry exists for this user, creating one from
        /// the DefaultUserProfile template on first sight. Call this whenever
        /// a user is about to be shown in the UI or evaluated for permission,
        /// so admins always see every known user in the matrix.
        /// </summary>
        public async Task<UserAccessEntry> EnsureUserAccessEntryAsync(User user)
        {
            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);
            var existing = settings.UserAccess.FirstOrDefault(u => u.UserId == user.InternalId);
            if (existing != null)
            {
                return existing;
            }

            var created = new UserAccessEntry
            {
                UserId = user.InternalId,
                UserName = user.Name,
                AllowSending = settings.DefaultProfile.AllowSending,
                AllowReceiving = settings.DefaultProfile.AllowReceiving,
                AllowedMediaTypes = settings.DefaultProfile.AllowedMediaTypes.ToList()
            };

            await this.adminSettingsStore.MutateAsync(s =>
            {
                if (!s.UserAccess.Any(u => u.UserId == user.InternalId))
                {
                    s.UserAccess.Add(created);
                }
            }).ConfigureAwait(false);

            return created;
        }

        public async Task<bool> CanSendAsync(User source, User target, string mediaType)
        {
            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);

            if (!IsInScope(settings.SendScope, settings.SendScopeUserIds, source.InternalId))
            {
                return false;
            }

            if (!IsInScope(settings.ReceiveScope, settings.ReceiveScopeUserIds, target.InternalId))
            {
                return false;
            }

            if (!settings.GloballyAllowedMediaTypes.Contains(mediaType))
            {
                return false;
            }

            var sourceEntry = await this.EnsureUserAccessEntryAsync(source).ConfigureAwait(false);
            if (!sourceEntry.AllowSending || !sourceEntry.AllowedMediaTypes.Contains(mediaType))
            {
                // Self-recommendation is always allowed once basic sending access exists.
                if (source.InternalId != target.InternalId)
                {
                    return false;
                }
            }

            var targetEntry = await this.EnsureUserAccessEntryAsync(target).ConfigureAwait(false);
            if (!targetEntry.AllowReceiving || !targetEntry.AllowedMediaTypes.Contains(mediaType))
            {
                return false;
            }

            if (source.InternalId == target.InternalId)
            {
                // Self-recommendations bypass the recipient opt-out layer below (nothing to opt out of).
                return true;
            }

            var preferences = await this.userPreferenceStore.GetForUserAsync(target.InternalId).ConfigureAwait(false);
            var senderPref = preferences.SenderPreferences.FirstOrDefault(p => p.SenderUserId == source.InternalId);
            if (senderPref != null && senderPref.OptedOutMediaTypes.Contains(mediaType))
            {
                return false;
            }

            return true;
        }

        private static bool IsInScope(AccessScope scope, System.Collections.Generic.List<long> allowList, long userId)
        {
            return scope == AccessScope.AllUsers || allowList.Contains(userId);
        }
    }
}
