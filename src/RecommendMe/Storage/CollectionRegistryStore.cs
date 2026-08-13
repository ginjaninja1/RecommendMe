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

        public Task RegisterAsync(
            long userId,
            string userName,
            long collectionId,
            string collectionName,
            string embyCollectionId)
        {
            return this.repository.MutateAsync(data =>
            {
                var existing = data.Entries.FirstOrDefault(entry => entry.UserId == userId);
                if (existing != null)
                {
                    existing.CollectionId = collectionId;
                    existing.UserName = userName;
                    existing.CollectionName = collectionName;
                    existing.EmbyCollectionId = embyCollectionId;
                    return;
                }

                data.Entries.Add(new CollectionRegistryEntry
                {
                    UserId = userId,
                    UserName = userName,
                    CollectionId = collectionId,
                    CollectionName = collectionName,
                    EmbyCollectionId = embyCollectionId
                });
            });
        }

        private static CollectionRegistryEntry Copy(CollectionRegistryEntry entry) => new CollectionRegistryEntry
        {
            UserId = entry.UserId,
            UserName = entry.UserName,
            CollectionId = entry.CollectionId,
            CollectionName = entry.CollectionName,
            EmbyCollectionId = entry.EmbyCollectionId
        };
    }
}
