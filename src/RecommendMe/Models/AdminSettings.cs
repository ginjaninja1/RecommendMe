using System.Collections.Generic;

namespace RecommendMe.Models
{
    /// <summary>
    /// Who a global permission applies to.
    /// </summary>
    public enum AccessScope
    {
        AllUsers,
        SpecificUsers
    }

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
    /// The template of permissions applied to a user the plugin has never
    /// seen before (i.e. no <see cref="UserAccessEntry"/> exists for them yet).
    /// Whenever a brand-new user is first evaluated, a UserAccessEntry is
    /// materialized for them from this template so admins can subsequently
    /// fine-tune it per-user.
    /// </summary>
    public class DefaultUserProfile
    {
        public bool AllowSending { get; set; } = true;

        public bool AllowReceiving { get; set; } = true;

        public List<string> AllowedMediaTypes { get; set; } = new List<string>(RecommendableMediaTypes.All);
    }

    /// <summary>
    /// Per-user override of the global permission matrix. Every user that has
    /// ever been evaluated by <see cref="Services.PermissionService"/> has one
    /// of these. "Emergency Revocation" is just AllowSending/AllowReceiving
    /// both set to false without touching anything else, so restoring access
    /// later doesn't lose the user's configured media type list.
    /// </summary>
    public class UserAccessEntry
    {
        public long UserId { get; set; }

        public string UserName { get; set; }

        public bool AllowSending { get; set; } = true;

        public bool AllowReceiving { get; set; } = true;

        public List<string> AllowedMediaTypes { get; set; } = new List<string>(RecommendableMediaTypes.All);
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
        /// <summary>Global rule for who is allowed to send recommendations at all.</summary>
        public AccessScope SendScope { get; set; } = AccessScope.AllUsers;

        /// <summary>User ids allowed to send, when SendScope == SpecificUsers.</summary>
        public List<long> SendScopeUserIds { get; set; } = new List<long>();

        /// <summary>Global rule for who is allowed to receive recommendations at all.</summary>
        public AccessScope ReceiveScope { get; set; } = AccessScope.AllUsers;

        /// <summary>User ids allowed to receive, when ReceiveScope == SpecificUsers.</summary>
        public List<long> ReceiveScopeUserIds { get; set; } = new List<long>();

        /// <summary>Media types recommendable at all, server-wide. Per-user AllowedMediaTypes is intersected with this.</summary>
        public List<string> GloballyAllowedMediaTypes { get; set; } = new List<string>(RecommendableMediaTypes.All);

        public DefaultUserProfile DefaultProfile { get; set; } = new DefaultUserProfile();

        public List<UserAccessEntry> UserAccess { get; set; } = new List<UserAccessEntry>();
    }
}
