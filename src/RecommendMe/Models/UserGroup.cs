using System;
using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal class UserGroup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public List<long> MemberUserIds { get; set; } = new List<long>();
    }
}
