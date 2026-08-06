using System.Collections.Generic;

namespace RecommendMe.Models
{
    /// <summary>
    /// One user's opt-out choices for a single sender they are permitted (by
    /// the admin matrix) to receive from. Absence of an entry for a media
    /// type means "still opted in" - entries only record explicit opt-outs,
    /// so a newly admin-granted sender/media-type starts opted-in by default.
    /// </summary>
    public class SenderPreference
    {
        public long SenderUserId { get; set; }

        public string SenderUserName { get; set; }

        /// <summary>Media types this user has explicitly opted OUT of receiving from this sender.</summary>
        public List<string> OptedOutMediaTypes { get; set; } = new List<string>();
    }

    /// <summary>
    /// A single user's Account-tab preferences: their per-sender, per-media-type
    /// opt-in/out choices. This is layered on top of (and can only narrow) the
    /// admin-defined permission matrix - it never grants access the admin
    /// hasn't already allowed.
    /// </summary>
    public class UserReceivePreferences
    {
        public long UserId { get; set; }

        public List<SenderPreference> SenderPreferences { get; set; } = new List<SenderPreference>();
    }
}
