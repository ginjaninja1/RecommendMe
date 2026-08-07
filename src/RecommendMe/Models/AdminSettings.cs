using System.Collections.Generic;

namespace RecommendMe.Models
{
    /// <summary>
    /// The set of Emby item kinds this plugin knows how to recommend.
    /// Kept as plain strings (matching MediaBrowser.Model.Entities.BaseItemKind
    /// names) rather than a hard dependency on the enum, so the JSON on disk
    /// stays stable even if the SDK's enum numbering ever changes.
    /// </summary>
    public static class RecommendableMediaTypes
    {
        public const string Movie = "Movie";
        public const string Series = "Series";
        public const string Season = "Season";
        public const string Episode = "Episode";
        public const string BoxSet = "BoxSet";
        public const string Person = "Person";
        public const string MusicArtist = "MusicArtist";
        public const string MusicAlbum = "MusicAlbum";
        public const string Song = "Audio";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Movie, Person, Series, Season, Episode, MusicArtist, MusicAlbum, Song
        };
    }

    /// <summary>
    /// Who a user is allowed to send recommendations to. This is the entire
    /// access model - there is no separate admin "receive scope": if A's send policy
    /// permits sending to B, B necessarily receives from A. See
    /// <see cref="Services.PermissionService"/> for evaluation order.
    /// </summary>
    public enum SendPolicyType
    {
        Everyone,
        NoOne,
        AllowedUsers,
        GroupMembers
    }

    public class UserGroup
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public List<long> MemberUserIds { get; set; } = new List<long>();
    }

    /// <summary>
    /// Per-user access record. Every user the plugin has ever evaluated has
    /// exactly one of these, copied from the configured default user the first
    /// time they're seen (see <see cref="Services.PermissionService.EnsureUserAccessEntryAsync"/>).
    ///
    /// <see cref="AccessSuspended"/> is the Emergency Revocation switch: it
    /// blocks this user from sending OR receiving, without touching their
    /// configured policy/AllowedTargetUserIds, so un-revoking restores
    /// exactly what was there before.
    /// </summary>
    public class UserAccessEntry
    {
        public long UserId { get; set; }

        public string UserName { get; set; }

        public SendPolicyType SendPolicy { get; set; } = SendPolicyType.NoOne;

        /// <summary>Target user ids this user may recommend to when AllowedUsers is active.</summary>
        public List<long> AllowedTargetUserIds { get; set; } = new List<long>();

        /// <summary>
        /// Whether users first discovered after this policy was configured are
        /// added to its allowed-user specification. The list is maintained even
        /// when AllowedUsers is not the active send policy.
        /// </summary>
        public bool AllowNewUsers { get; set; }

        /// <summary>Emergency Revocation: true blocks all sending and receiving for this user.</summary>
        public bool AccessSuspended { get; set; } = false;
    }

    /// <summary>
    /// All admin-configured settings for the plugin. Persisted as JSON under
    /// ProgramData\data\RecommendMe\admin-settings.json (see
    /// <see cref="Storage.AdminSettingsStore"/>) rather than through Emby's
    /// BasePlugin&lt;T&gt; XML mechanism, per the explicit storage requirement
    /// that all plugin data live under that JSON data directory.
    /// </summary>
    public class AdminSettings
    {
        /// <summary>
        /// Server-wide media types the Recommend UI is allowed to offer at all.
        /// This is NOT a per-user access control - it exists purely so the
        /// recommend picker doesn't let a user pick an Emby item type that
        /// can't actually be added to a Collection (or that the admin has
        /// otherwise decided not to support). Every user sees the same list.
        /// </summary>
        public List<string> GloballyAllowedMediaTypes { get; set; } = new List<string>(RecommendableMediaTypes.All);

        /// <summary>
        /// Existing user whose groups, send policy, allowed-user specification,
        /// and new-user behavior are copied when a new user is first seen.
        /// </summary>
        public long? DefaultUserPolicySourceUserId { get; set; }

        /// <summary>
        /// When enabled, blank user/group filters immediately show their first
        /// page. Disable it on large configurations to require a search term.
        /// </summary>
        public bool AlwaysExpandUsersAndGroups { get; set; } = true;

        public List<UserAccessEntry> UserAccess { get; set; } = new List<UserAccessEntry>();

        public List<UserGroup> Groups { get; set; } = new List<UserGroup>();
    }
}
