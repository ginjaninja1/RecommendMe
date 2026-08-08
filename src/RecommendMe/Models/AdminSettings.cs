using System.Collections.Generic;

namespace RecommendMe.Models
{
    internal class AdminSettings
    {
        public AdminSettings()
        {
        }

        public const string DefaultRecommendationCollectionPrefix = "_Recommended_";

        public List<string> GloballyAllowedMediaTypes { get; set; } = new List<string>(RecommendableMediaTypes.All);

        public long? DefaultUserPolicySourceUserId { get; set; }

        public bool AlwaysExpandUsersAndGroups { get; set; } = true;

        public bool ClearWatchedRecommendations { get; set; }

        public bool PreventWatchedRecommendations { get; set; } = true;

        public string RecommendationCollectionPrefix { get; set; } = DefaultRecommendationCollectionPrefix;

        public string RecommendationCollectionSuffix { get; set; } = string.Empty;

        public List<UserAccessEntry> UserAccess { get; set; } = new List<UserAccessEntry>();

        public List<UserGroup> Groups { get; set; } = new List<UserGroup>();
    }
}
