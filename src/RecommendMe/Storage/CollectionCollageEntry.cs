using System;

namespace RecommendMe.Storage
{
    internal class CollectionCollageEntry
    {
        public long ItemId { get; set; }

        public DateTimeOffset AddedUtc { get; set; }
    }
}