using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace RecommendMe.Storage
{
    internal class CollectionRegistryStore
    {
        private readonly JsonFileRepository<CollectionRegistryData> repository;

        public CollectionRegistryStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<CollectionRegistryData>(
                RecommendMeDataPaths.CollectionRegistryFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public async Task<CollectionRegistryEntry?> GetAsync(long userId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            var entry = data.Entries.FirstOrDefault(candidate => candidate.UserId == userId);
            return entry == null ? null : Copy(entry);
        }

        public async Task<List<CollectionRegistryEntry>> GetAllAsync()
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            return data.Entries.Select(Copy).ToList();
        }

        public Task RegisterAsync(long userId, long collectionId, string embyCollectionId)
        {
            return this.repository.MutateAsync(data =>
            {
                var existing = data.Entries.FirstOrDefault(entry => entry.UserId == userId);
                if (existing != null)
                {
                    existing.CollectionId = collectionId;
                    existing.EmbyCollectionId = embyCollectionId;
                    return;
                }

                data.Entries.Add(new CollectionRegistryEntry
                {
                    UserId = userId,
                    CollectionId = collectionId,
                    EmbyCollectionId = embyCollectionId
                });
            });
        }

        private static CollectionRegistryEntry Copy(CollectionRegistryEntry entry) => new CollectionRegistryEntry
        {
            UserId = entry.UserId,
            CollectionId = entry.CollectionId,
            EmbyCollectionId = entry.EmbyCollectionId
        };
    }
}
