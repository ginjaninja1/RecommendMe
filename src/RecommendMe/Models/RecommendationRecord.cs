using System;

namespace RecommendMe.Models
{
    internal class RecommendationRecord
    {
        /// <summary>SentByUserName sentinel for a recommendation recorded from an out-of-plugin collection add (no real sending user).</summary>
        public const string SystemSenderName = "System";

        public Guid RecommendationId { get; set; } = Guid.NewGuid();

        public long ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string MediaType { get; set; } = string.Empty;

        public long SentByUserId { get; set; }

        public string SentByUserName { get; set; } = string.Empty;

        public long SentToUserId { get; set; }

        public string SentToUserName { get; set; } = string.Empty;

        public DateTime DateSentUtc { get; set; } = DateTime.UtcNow;

        public bool IsPrivate { get; set; }

        public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;

        public DateTime? DateResolvedUtc { get; set; }
    }
}