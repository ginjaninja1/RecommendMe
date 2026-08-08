using System;

namespace RecommendMe.Models
{
    /// <summary>A recommendation notification queued because the recipient had no active session at send time.</summary>
    internal class PendingNotificationRecord
    {
        public string NotificationId { get; set; } = Guid.NewGuid().ToString("N");

        public long RecipientUserId { get; set; }

        public string RecipientUserName { get; set; }

        public string MessageText { get; set; }

        public DateTime QueuedUtc { get; set; } = DateTime.UtcNow;
    }
}