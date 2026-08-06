using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Owns creation and membership of each user's native Emby Collection
    /// named "_Recommended_{username}". This service is strictly responsible
    /// for adding/removing the item from that collection - it never touches
    /// the RecommendationRecord metadata (that's RecommendationService's job)
    /// and never forces a browser/view refresh; Emby's own collection
    /// rendering handles that.
    /// </summary>
    public class CollectionSyncService
    {
        private readonly ICollectionManager collectionManager;
        private readonly ILibraryManager libraryManager;
        private readonly CollectionRegistryStore registryStore;
        private readonly ILogger logger;

        public CollectionSyncService(
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            CollectionRegistryStore registryStore,
            ILogger logger)
        {
            this.collectionManager = collectionManager;
            this.libraryManager = libraryManager;
            this.registryStore = registryStore;
            this.logger = logger;
        }

        private static string CollectionNameFor(User user) => $"_Recommended_{user.Name}";

        /// <summary>
        /// Gets the recipient's recommendation collection, creating it (and
        /// registering it) the first time they ever receive a recommendation.
        /// </summary>
        public async Task<BoxSet> GetOrCreateCollectionAsync(User recipient)
        {
            var existingId = await this.registryStore.GetCollectionIdAsync(recipient.InternalId).ConfigureAwait(false);
            if (existingId.HasValue)
            {
                var existingItem = this.libraryManager.GetItemById(existingId.Value) as BoxSet;
                if (existingItem != null)
                {
                    return existingItem;
                }

                this.logger.Warn(
                    "RecommendMe: registered collection id {0} for user {1} no longer resolves to a BoxSet; recreating.",
                    existingId.Value,
                    recipient.Name);
            }

            var name = CollectionNameFor(recipient);

            var created = await this.collectionManager.CreateCollection(new CollectionCreationOptions
            {
                Name = name,
                UserIds = new[] { recipient.InternalId }
            }).ConfigureAwait(false);

            await this.registryStore.RegisterAsync(recipient.InternalId, created.InternalId, name).ConfigureAwait(false);

            return created;
        }

        public async Task AddItemAsync(User recipient, BaseItem item)
        {
            var collection = await this.GetOrCreateCollectionAsync(recipient).ConfigureAwait(false);
            await this.collectionManager.AddToCollection(collection.InternalId, new[] { item.InternalId }).ConfigureAwait(false);
        }

        public async Task RemoveItemAsync(User recipient, long itemId)
        {
            var collectionId = await this.registryStore.GetCollectionIdAsync(recipient.InternalId).ConfigureAwait(false);
            if (!collectionId.HasValue)
            {
                return;
            }

            var collection = this.libraryManager.GetItemById(collectionId.Value) as BoxSet;
            if (collection == null)
            {
                return;
            }

            this.collectionManager.RemoveFromCollection(collection, new[] { itemId });
        }
    }
}
