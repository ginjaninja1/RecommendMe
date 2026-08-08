using System.Collections.Generic;

namespace RecommendMe.Storage
{
    internal class CollectionCollageState
    {
        public long CollectionId { get; set; }

        /// <summary>
        /// Add-order entries, newest first is NOT guaranteed by storage order -
        /// callers must sort by AddedUtc. Capped at CollectionCollageStore.MaxTrackedItems
        /// so removals near the front of the recency list can still be backfilled
        /// from history without the file growing unbounded.
        /// </summary>
        public List<CollectionCollageEntry> RecentItems { get; set; } = new List<CollectionCollageEntry>();

        /// <summary>The item id set used to build the currently-applied collage image, for change detection.</summary>
        public List<long> LastBuiltItemIds { get; set; } = new List<long>();
    }
}