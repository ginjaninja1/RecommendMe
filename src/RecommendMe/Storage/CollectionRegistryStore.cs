using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace RecommendMe.Storage
{
    public class CollectionRegistryEntry
    {
        public long UserId { get; set; }

        public long CollectionId { get; set; }

        public string CollectionName { get; set; }
    }

    public class CollectionRegistryData
    {
        public List<CollectionRegistryEntry> Entries { get; set; } = new List<CollectionRegistryEntry>();
    }

    /// <summary>
    /// Maps each user to the internal Emby BoxSet (Collection) id of their
    /// "_Recommended_username" collection, so we never have to search for it
    /// by name and never accidentally create duplicates.
    /// </summary>
    public class CollectionRegistryStore
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

        public async Task<long?> GetCollectionIdAsync(long userId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            var entry = data.Entries.FirstOrDefault(e => e.UserId == userId);
            return entry?.CollectionId;
        }

        public Task RegisterAsync(long userId, long collectionId, string collectionName)
        {
            return this.repository.MutateAsync(data =>
            {
                var existing = data.Entries.FirstOrDefault(e => e.UserId == userId);
                if (existing != null)
                {
                    existing.CollectionId = collectionId;
                    existing.CollectionName = collectionName;
                }
                else
                {
                    data.Entries.Add(new CollectionRegistryEntry
                    {
                        UserId = userId,
                        CollectionId = collectionId,
                        CollectionName = collectionName
                    });
                }
            });
        }
    }
}
