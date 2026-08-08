using System.Collections.Generic;

namespace RecommendMe.Storage
{
    internal sealed class CollectionRegistryData
    {
        public CollectionRegistryData()
        {
        }

        public List<CollectionRegistryEntry> Entries { get; set; } = new List<CollectionRegistryEntry>();
    }
}
