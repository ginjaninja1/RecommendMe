using System.Collections.Generic;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    internal sealed class PendingNotificationCollection
    {
        public PendingNotificationCollection()
        {
        }

        public List<PendingNotificationRecord> Records { get; set; } = new List<PendingNotificationRecord>();
    }
}