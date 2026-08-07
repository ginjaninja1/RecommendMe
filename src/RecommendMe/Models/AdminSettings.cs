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
        public const string MusicArtist = "MusicArtist";
        public const string MusicAlbum = "MusicAlbum";
        public const string Song = "Audio";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Movie, Series, Season, Episode, BoxSet, MusicArtist, MusicAlbum, Song
        };
    }

    /// <summary>
    /// Who a user is allowed to send recommendations to. This is the entire
    /// access model - there is no separate "receive scope": if A's SendMode
    /// permits sending to B, B necessarily receives from A. See
    /// <see cref="Services.PermissionService"/> for evaluation order.
    /// </summary>
    public enum SendMode
    {
        Everyone,
        NoOne,
        SpecificUsers
    }

    /// <summary>
    /// Per-user access record. Every user the plugin has ever evaluated has
    /// exactly one of these, materialized from <see cref="AdminSettings.NewUserDefaultSendMode"/>
    /// the first time they're seen (see <see cref="Services.PermissionService.EnsureUserAccessEntryAsync"/>).
    ///
    /// <see cref="AccessSuspended"/> is the Emergency Revocation switch: it
    /// blocks this user from sending OR receiving, without touching their
    /// configured SendMode/AllowedTargetUserIds, so un-revoking restores
    /// exactly what was there before.
    /// </summary>
    public class UserAccessEntry
    {
        public long UserId { get; set; }

        public string UserName { get; set; }

        public SendMode SendMode { get; set; } = SendMode.Everyone;

        /// <summary>Target user ids this user may recommend to, when SendMode == SpecificUsers.</summary>
        public List<long> AllowedTargetUserIds { get; set; } = new List<long>();

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
        /// SendMode a brand-new user's UserAccessEntry is created with.
        /// Only Everyone or NoOne are meaningful defaults here - the admin UI
        /// does not offer SpecificUsers for this setting, since a new user
        /// has no target list to speak of yet.
        /// </summary>
        public SendMode NewUserDefaultSendMode { get; set; } = SendMode.Everyone;

        /// <summary>
        /// When a new user is first seen, should they automatically be added
        /// to every existing SpecificUsers-mode user's AllowedTargetUserIds?
        /// True = new users are auto-included as a valid recommend target for
        /// everyone already using SpecificUsers mode. False = existing users'
        /// named lists are left untouched and the admin must add the new user
        /// manually if desired.
        /// </summary>
        public bool AutoGrantNewUsersToExistingSendLists { get; set; } = true;

        public List<UserAccessEntry> UserAccess { get; set; } = new List<UserAccessEntry>();
    }
}