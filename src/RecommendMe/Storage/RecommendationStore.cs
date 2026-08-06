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
    /// Wrapper type so JsonFileRepository&lt;T&gt;'s `new()` constraint is
    /// satisfiable for a bare list.
    /// </summary>
    public class RecommendationRecordCollection
    {
        public List<RecommendationRecord> Records { get; set; } = new List<RecommendationRecord>();
    }

    /// <summary>
    /// Persists the full recommendation history/log to recommendations.json.
    /// This is the single source of truth used by the history grid, the
    /// duplicate-recommendation pre-check, and the watched-cleanup hook.
    /// </summary>
    public class RecommendationStore
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

        public async Task<bool> HasActiveRecommendationAsync(long recipientUserId, long itemId)
        {
            var all = await this.GetAllAsync().ConfigureAwait(false);

            foreach (var record in all)
            {
                if (record.SentToUserId == recipientUserId
                    && record.ItemId == itemId
                    && record.Status == RecommendationStatus.Active)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
