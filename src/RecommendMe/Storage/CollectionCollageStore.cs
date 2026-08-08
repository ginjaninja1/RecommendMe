using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Tracks, per Emby Collection, the add-order of its members (Emby's
    /// BoxSet exposes no "date added to collection" concept - confirmed
    /// against the 4.10.0.22 decompile, sorting is DisplayOrder/SortName/
    /// ProductionYear only) and which item set the currently-applied collage
    /// image was built from. This is the sole source of truth
    /// CollectionCollageBuilder uses to pick the 4 most recently added items.
    /// </summary>
    internal class CollectionCollageStore
    {
        /// <summary>
        /// Keep more than 4 so a removal near the front of the recency list
        /// can still be backfilled from history instead of the collage
        /// dropping straight to 3 images.
        /// </summary>
        private const int MaxTrackedItems = 12;

        private readonly JsonFileRepository<CollectionCollageData> repository;

        public CollectionCollageStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<CollectionCollageData>(
                RecommendMeDataPaths.CollectionCollagesFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public Task RecordItemAddedAsync(long collectionId, long itemId, System.DateTimeOffset addedUtc)
        {
            return this.repository.MutateAsync(data =>
            {
                var state = GetOrCreateState(data, collectionId);

                state.RecentItems.RemoveAll(entry => entry.ItemId == itemId);
                state.RecentItems.Add(new CollectionCollageEntry { ItemId = itemId, AddedUtc = addedUtc });

                if (state.RecentItems.Count > MaxTrackedItems)
                {
                    state.RecentItems = state.RecentItems
                        .OrderByDescending(entry => entry.AddedUtc)
                        .Take(MaxTrackedItems)
                        .ToList();
                }
            });
        }

        public Task RecordItemRemovedAsync(long collectionId, long itemId)
        {
            return this.repository.MutateAsync(data =>
            {
                var state = data.States.FirstOrDefault(candidate => candidate.CollectionId == collectionId);
                state?.RecentItems.RemoveAll(entry => entry.ItemId == itemId);
            });
        }

        /// <summary>Returns tracked item ids ordered most-recently-added first.</summary>
        public async Task<List<long>> GetRecentItemIdsAsync(long collectionId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            var state = data.States.FirstOrDefault(candidate => candidate.CollectionId == collectionId);
            if (state == null)
            {
                return new List<long>();
            }

            return state.RecentItems
                .OrderByDescending(entry => entry.AddedUtc)
                .Select(entry => entry.ItemId)
                .ToList();
        }

        public async Task<List<long>> GetLastBuiltItemIdsAsync(long collectionId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            var state = data.States.FirstOrDefault(candidate => candidate.CollectionId == collectionId);
            return state == null ? new List<long>() : new List<long>(state.LastBuiltItemIds);
        }

        public Task SetLastBuiltItemIdsAsync(long collectionId, IEnumerable<long> itemIds)
        {
            return this.repository.MutateAsync(data =>
            {
                var state = GetOrCreateState(data, collectionId);
                state.LastBuiltItemIds = itemIds.ToList();
            });
        }

        private static CollectionCollageState GetOrCreateState(CollectionCollageData data, long collectionId)
        {
            var state = data.States.FirstOrDefault(candidate => candidate.CollectionId == collectionId);
            if (state != null)
            {
                return state;
            }

            state = new CollectionCollageState { CollectionId = collectionId };
            data.States.Add(state);
            return state;
        }
    }
}