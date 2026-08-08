using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal class UserAccessEntry
    {
        public long UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public SendPolicy SendPolicy { get; set; } = SendPolicy.NoOne;

        public List<long> AllowedTargetUserIds { get; set; } = new List<long>();

        public bool AllowNewUsers { get; set; }

        public bool AccessSuspended { get; set; }
    }
}
