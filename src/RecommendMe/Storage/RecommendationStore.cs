using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Persists the recommendation history log to recommendations.json. This
    /// is write-only history for the History dialog and the watched-cleanup
    /// hook (ResolveWatchedAsync); it is NOT consulted for submission-time
    /// gating - that check runs against the live collection instead (see
    /// CollectionSyncService.IsItemInRecipientCollectionAsync /
    /// RecommendationService.SendRecommendationAsync) because this log's
    /// Active status can go stale (e.g. a user manually removes an item from
    /// their recommendation collection outside the watched flow).
    /// </summary>
    internal class RecommendationStore
    {
        private readonly JsonFileRepository<RecommendationRecordCollection> repository;

        public RecommendationStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<RecommendationRecordCollection>(
                RecommendMeDataPaths.RecommendationsFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public async Task<List<RecommendationRecord>> GetAllAsync()
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            return data.Records;
        }

        public Task AddAsync(RecommendationRecord record)
        {
            return this.repository.MutateAsync(data => data.Records.Add(record));
        }

        /// <summary>
        /// Marks every Active record for the given (userId, itemId) pair as
        /// AutoRemovedWatched. Returns the records that were changed, so the
        /// caller can remove those items from the user's Emby collection.
        /// </summary>
        public async Task<List<RecommendationRecord>> ResolveWatchedAsync(long recipientUserId, long itemId)
        {
            var changed = new List<RecommendationRecord>();

            await this.repository.MutateAsync(data =>
            {
                foreach (var record in data.Records)
                {
                    if (record.SentToUserId == recipientUserId
                        && record.ItemId == itemId
                        && record.Status == RecommendationStatus.Active)
                    {
                        record.Status = RecommendationStatus.AutoRemovedWatched;
                        record.DateResolvedUtc = DateTime.UtcNow;
                        changed.Add(record);
                    }
                }
            }).ConfigureAwait(false);

            return changed;
        }
    }
}
