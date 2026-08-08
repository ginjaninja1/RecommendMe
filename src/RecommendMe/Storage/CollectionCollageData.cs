using System.Collections.Generic;

namespace RecommendMe.Storage
{
    internal sealed class CollectionCollageData
    {
        public CollectionCollageData()
        {
        }

        public List<CollectionCollageState> States { get; set; } = new List<CollectionCollageState>();
    }
}