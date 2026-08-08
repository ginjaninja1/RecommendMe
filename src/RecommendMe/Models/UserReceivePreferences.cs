using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal class UserReceivePreferences
    {
        public long UserId { get; set; }

        public List<SenderPreference> SenderPreferences { get; set; } = new List<SenderPreference>();
    }
}
