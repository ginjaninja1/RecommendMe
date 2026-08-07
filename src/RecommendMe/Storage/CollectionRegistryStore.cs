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

        /// <summary>Stable public Emby item id, stored alongside the internal database id for validation.</summary>
        public string EmbyCollectionId { get; set; }
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
            var entry = await this.GetAsync(userId).ConfigureAwait(false);
            return entry?.CollectionId;
        }

        public async Task<CollectionRegistryEntry> GetAsync(long userId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            var entry = data.Entries.FirstOrDefault(e => e.UserId == userId);
            return entry == null ? null : new CollectionRegistryEntry
            {
                UserId = entry.UserId,
                CollectionId = entry.CollectionId,
                CollectionName = entry.CollectionName,
                EmbyCollectionId = entry.EmbyCollectionId
            };
        }

        public async Task<List<CollectionRegistryEntry>> GetAllAsync()
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            return data.Entries.Select(e => new CollectionRegistryEntry
            {
                UserId = e.UserId,
                CollectionId = e.CollectionId,
                CollectionName = e.CollectionName,
                EmbyCollectionId = e.EmbyCollectionId
            }).ToList();
        }

        public Task RegisterAsync(long userId, long collectionId, string collectionName, string embyCollectionId = null)
        {
            return this.repository.MutateAsync(data =>
            {
                var existing = data.Entries.FirstOrDefault(e => e.UserId == userId);
                if (existing != null)
                {
                    existing.CollectionId = collectionId;
                    existing.CollectionName = collectionName;
                    existing.EmbyCollectionId = embyCollectionId ?? existing.EmbyCollectionId;
                }
                else
                {
                    data.Entries.Add(new CollectionRegistryEntry
                    {
                        UserId = userId,
                        CollectionId = collectionId,
                        CollectionName = collectionName,
                        EmbyCollectionId = embyCollectionId
                    });
                }
            });
        }
    }
}
