using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal class SenderPreference
    {
        public long SenderUserId { get; set; }

        public bool Blocked { get; set; }

        public List<string> OptedOutMediaTypes { get; set; } = new List<string>();
    }
}
