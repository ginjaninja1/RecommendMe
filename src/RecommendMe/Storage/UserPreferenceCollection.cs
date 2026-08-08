using System.Collections.Generic;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    internal sealed class UserPreferenceCollection
    {
        public UserPreferenceCollection()
        {
        }

        public List<UserReceivePreferences> Users { get; set; } = new List<UserReceivePreferences>();
    }
}
