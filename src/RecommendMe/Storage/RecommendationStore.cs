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
    /// Persists the recommendation event log to recommendations.json. It is
    /// not collection state and is not consulted or changed by membership
    /// reconciliation. Submission-time gating runs against the live collection (see
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

    }
}
