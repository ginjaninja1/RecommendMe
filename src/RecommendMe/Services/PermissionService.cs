using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Why a send was or wasn't permitted. AdminBlocked covers every
    /// server-wide/admin-matrix reason (unsupported media type, Emergency
    /// Revocation, send policy) - these are intentionally not detailed further
    /// to the sender, since they're the admin's policy, not the recipient's.
    /// The Recipient* values are the recipient's own Account-tab choice, and
    /// are surfaced back to the sender by name (see RecommendPageView).
    /// </summary>
    public enum SendPermissionResult
    {
        Allowed,
        AdminBlocked,
        RecipientBlockedSender,
        RecipientOptedOutMediaType
    }

    /// <summary>
    /// Resolves whether a source user may send a recommendation of a given
    /// media type to a target user. Combines the admin-configured global
    /// media type list, the sender's own send policy/AllowedTargetUserIds
    /// (copied from the selected default user the first time a user is seen),
    /// either side's Emergency Revocation
    /// flag, and the recipient's own Account-tab opt-outs (<see cref="UserPreferenceStore"/>).
    ///
    /// Resolution order (most to least restrictive wins):
    /// 1. GloballyAllowedMediaTypes (server-wide - the type must be offerable at all)
    /// 2. AccessSuspended on either side (Emergency Revocation)
    /// 3. Source's send policy (Everyone / NoOne / AllowedUsers / GroupMembers)
    /// 4. Recipient's own master Blocked switch for this sender
    /// 5. Recipient's own opt-out of this specific sender/media-type
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
        /// Whether the administrator has suspended all RecommendMe access for
        /// this user. User-facing pages use this as their common page and
        /// command boundary; domain operations still enforce their own rules.
        /// </summary>
        public async Task<bool> IsAccessSuspendedAsync(User user)
        {
            var entry = await this.EnsureUserAccessEntryAsync(user).ConfigureAwait(false);
            return entry.AccessSuspended;
        }

        /// <summary>
        /// Ensures a UserAccessEntry exists for this user, copying the selected
        /// default user's send policy and groups on first sight. Existing users
        /// that opt into new recipients also receive the new id in their stored
        /// allowed-user specification. Call this whenever a user is about to
        /// be shown in the UI or evaluated for permission, so admins always
        /// see every known user in the matrix.
        /// </summary>
        public async Task<UserAccessEntry> EnsureUserAccessEntryAsync(User user)
        {
            var snapshot = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);
            var existing = snapshot.UserAccess.FirstOrDefault(u => u.UserId == user.InternalId);
            if (existing != null)
            {
                if (!string.Equals(existing.UserName, user.Name, System.StringComparison.Ordinal))
                {
                    await this.adminSettingsStore.MutateAsync(s =>
                    {
                        var renamed = s.UserAccess.FirstOrDefault(u => u.UserId == user.InternalId);
                        if (renamed != null) renamed.UserName = user.Name;
                    }).ConfigureAwait(false);
                    existing.UserName = user.Name;
                }

                return existing;
            }

            UserAccessEntry result = null;
            await this.adminSettingsStore.MutateAsync(s =>
            {
                result = s.UserAccess.FirstOrDefault(u => u.UserId == user.InternalId);
                if (result != null)
                {
                    result.UserName = user.Name;
                    return;
                }

                var template = s.DefaultUserPolicySourceUserId.HasValue
                    ? s.UserAccess.FirstOrDefault(u => u.UserId == s.DefaultUserPolicySourceUserId.Value)
                    : null;

                result = new UserAccessEntry
                {
                    UserId = user.InternalId,
                    UserName = user.Name,
                    SendPolicy = template?.SendPolicy ?? SendPolicyType.NoOne,
                    AllowNewUsers = template?.AllowNewUsers ?? false,
                    AllowedTargetUserIds = template == null
                        ? new System.Collections.Generic.List<long>()
                        : new System.Collections.Generic.List<long>(template.AllowedTargetUserIds)
                };

                foreach (var existingEntry in s.UserAccess.Where(e => e.AllowNewUsers))
                {
                    if (!existingEntry.AllowedTargetUserIds.Contains(user.InternalId))
                    {
                        existingEntry.AllowedTargetUserIds.Add(user.InternalId);
                    }
                }

                if (template != null)
                {
                    foreach (var group in s.Groups.Where(g => g.MemberUserIds.Contains(template.UserId)))
                    {
                        if (!group.MemberUserIds.Contains(user.InternalId))
                        {
                            group.MemberUserIds.Add(user.InternalId);
                        }
                    }
                }

                s.UserAccess.Add(result);
            }).ConfigureAwait(false);

            return result;
        }

        public async Task<SendPermissionResult> CanSendAsync(User source, User target, string mediaType)
        {
            if (mediaType == RecommendableMediaTypes.BoxSet)
            {
                return SendPermissionResult.AdminBlocked;
            }

            await this.EnsureUserAccessEntryAsync(source).ConfigureAwait(false);
            await this.EnsureUserAccessEntryAsync(target).ConfigureAwait(false);
            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);
            var sourceEntry = settings.UserAccess.First(u => u.UserId == source.InternalId);
            var targetEntry = settings.UserAccess.First(u => u.UserId == target.InternalId);

            if (!settings.GloballyAllowedMediaTypes.Contains(mediaType))
            {
                return SendPermissionResult.AdminBlocked;
            }

            if (sourceEntry.AccessSuspended || targetEntry.AccessSuspended)
            {
                return SendPermissionResult.AdminBlocked;
            }

            if (source.InternalId == target.InternalId)
            {
                // Self-recommendation always allowed once basic access exists (i.e. not suspended).
                return SendPermissionResult.Allowed;
            }

            if (!IsTargetAllowed(sourceEntry, target.InternalId, settings))
            {
                return SendPermissionResult.AdminBlocked;
            }

            var preferences = await this.userPreferenceStore.GetForUserAsync(target.InternalId).ConfigureAwait(false);
            var senderPref = preferences.SenderPreferences.FirstOrDefault(p => p.SenderUserId == source.InternalId);

            if (senderPref != null && senderPref.Blocked)
            {
                return SendPermissionResult.RecipientBlockedSender;
            }

            if (senderPref != null && senderPref.OptedOutMediaTypes.Contains(mediaType))
            {
                return SendPermissionResult.RecipientOptedOutMediaType;
            }

            return SendPermissionResult.Allowed;
        }

        /// <summary>
        /// Whether sourceEntry's send policy permits sending to targetUserId.
        /// Internal (not private) so other UI-layer builders that need the
        /// same "would this currently be allowed" check - e.g. HistoryViewBuilder's
        /// third-party visibility filter, RecommendViewBuilder's target picker -
        /// can reuse this instead of re-implementing the policy switch.
        /// </summary>
        internal static bool IsTargetAllowed(UserAccessEntry sourceEntry, long targetUserId, AdminSettings settings)
        {
            switch (sourceEntry.SendPolicy)
            {
                case SendPolicyType.Everyone:
                    return true;
                case SendPolicyType.NoOne:
                    return false;
                case SendPolicyType.AllowedUsers:
                    return sourceEntry.AllowedTargetUserIds.Contains(targetUserId);
                case SendPolicyType.GroupMembers:
                    return settings.Groups.Any(g => g.MemberUserIds.Contains(sourceEntry.UserId) && g.MemberUserIds.Contains(targetUserId));
                default:
                    return false;
            }
        }
    }
}
