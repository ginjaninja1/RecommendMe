using System;

namespace RecommendMe.Models
{
    /// <summary>
    /// Status of a single recommendation. A recommendation is "Active" from the
    /// moment it is sent until it is either watched by the recipient (at which
    /// point it is auto-removed from their collection) or manually cleared.
    /// </summary>
    public enum RecommendationStatus
    {
        Active,
        AutoRemovedWatched
    }

    /// <summary>
    /// One row of recommendation metadata. This is the persisted, authoritative
    /// record of "who recommended what to whom" - the native Emby Collection
    /// only holds the item itself; every other fact (sender, recipient, privacy,
    /// timestamps, status) lives here.
    /// </summary>
    public class RecommendationRecord
    {
        /// <summary>Unique id for this recommendation (Guid, assigned on creation).</summary>
        public Guid RecommendationId { get; set; } = Guid.NewGuid();

        public long ItemId { get; set; }

        public string ItemName { get; set; }

        /// <summary>Emby BaseItemKind, stored as string (e.g. "Movie", "Series").</summary>
        public string MediaType { get; set; }

        public long SentByUserId { get; set; }

        public string SentByUserName { get; set; }

        public long SentToUserId { get; set; }

        public string SentToUserName { get; set; }

        public DateTime DateSentUtc { get; set; } = DateTime.UtcNow;

        public bool IsPrivate { get; set; }

        public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;

        /// <summary>Set when Status transitions away from Active.</summary>
        public DateTime? DateResolvedUtc { get; set; }
    }
}
