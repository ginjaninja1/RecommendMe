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
    /// Revocation, SendMode) - these are intentionally not detailed further
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
    /// media type list, the sender's own SendMode/AllowedTargetUserIds
    /// (materialized from <see cref="AdminSettings.NewUserDefaultSendMode"/>
    /// the first time a user is seen), either side's Emergency Revocation
    /// flag, and the recipient's own Account-tab opt-outs (<see cref="UserPreferenceStore"/>).
    ///
    /// Resolution order (most to least restrictive wins):
    /// 1. GloballyAllowedMediaTypes (server-wide - the type must be offerable at all)
    /// 2. AccessSuspended on either side (Emergency Revocation)
    /// 3. Source's SendMode (Everyone / NoOne / SpecificUsers allow-list)
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
        /// Ensures a UserAccessEntry exists for this user, creating one from
        /// NewUserDefaultSendMode on first sight. When AutoGrantNewUsersToExistingSendLists
        /// is set, also adds this new user to every existing SpecificUsers-mode
        /// user's AllowedTargetUserIds. Call this whenever a user is about to
        /// be shown in the UI or evaluated for permission, so admins always
        /// see every known user in the matrix.
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
                SendMode = settings.NewUserDefaultSendMode
            };

            await this.adminSettingsStore.MutateAsync(s =>
            {
                if (s.UserAccess.Any(u => u.UserId == user.InternalId))
                {
                    return;
                }

                s.UserAccess.Add(created);

                if (s.AutoGrantNewUsersToExistingSendLists)
                {
                    foreach (var existingEntry in s.UserAccess)
                    {
                        if (existingEntry.UserId != created.UserId
                            && existingEntry.SendMode == SendMode.SpecificUsers
                            && !existingEntry.AllowedTargetUserIds.Contains(created.UserId))
                        {
                            existingEntry.AllowedTargetUserIds.Add(created.UserId);
                        }
                    }
                }
            }).ConfigureAwait(false);

            return created;
        }

        public async Task<SendPermissionResult> CanSendAsync(User source, User target, string mediaType)
        {
            if (mediaType == RecommendableMediaTypes.BoxSet)
            {
                return SendPermissionResult.AdminBlocked;
            }

            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);

            if (!settings.GloballyAllowedMediaTypes.Contains(mediaType))
            {
                return SendPermissionResult.AdminBlocked;
            }

            var sourceEntry = await this.EnsureUserAccessEntryAsync(source).ConfigureAwait(false);
            var targetEntry = await this.EnsureUserAccessEntryAsync(target).ConfigureAwait(false);

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
        /// Whether sourceEntry's SendMode permits sending to targetUserId.
        /// Internal (not private) so other UI-layer builders that need the
        /// same "would this currently be allowed" check - e.g. HistoryViewBuilder's
        /// third-party visibility filter, RecommendViewBuilder's target picker -
        /// can reuse this instead of re-implementing the SendMode switch.
        /// </summary>
        internal static bool IsTargetAllowed(UserAccessEntry sourceEntry, long targetUserId, AdminSettings settings)
        {
            switch (sourceEntry.SendMode)
            {
                case SendMode.Everyone:
                    return true;
                case SendMode.NoOne:
                    return false;
                case SendMode.SpecificUsers:
                    return sourceEntry.AllowedTargetUserIds.Contains(targetUserId);
                case SendMode.MyGroups:
                    return settings.Groups.Any(g => g.MemberUserIds.Contains(sourceEntry.UserId) && g.MemberUserIds.Contains(targetUserId));
                default:
                    return false;
            }
        }
    }
}
