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
        /// <param name="recipient">The collection owner.</param>
        /// <param name="seedItem">
        /// The item to create the collection from. Required on first creation:
        /// CollectionManager.CreateCollection (Emby core) only builds a BoxSet
        /// by walking options.ItemIdList - with an empty list it silently
        /// returns a null BoxSet (no exception), which is what caused the
        /// NullReferenceException on created.InternalId below. UserIds is not
        /// read anywhere in CreateCollection's implementation - it does not
        /// scope collection membership/visibility - so it's dropped entirely.
        /// </param>
        public async Task<BoxSet> GetOrCreateCollectionAsync(User recipient, BaseItem seedItem)
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
                ItemIdList = new[] { seedItem.InternalId }
            }).ConfigureAwait(false);

            if (created == null)
            {
                // CreateCollection returns null (not a thrown exception) if it
                // couldn't resolve seedItem via ILibraryManager.GetItemById -
                // surface that plainly instead of NRE-ing on created.InternalId.
                throw new System.InvalidOperationException(
                    $"RecommendMe: CreateCollection returned no BoxSet for '{name}' (seed item {seedItem.InternalId}).");
            }

            await this.registryStore.RegisterAsync(recipient.InternalId, created.InternalId, name).ConfigureAwait(false);

            return created;
        }

        public async Task AddItemAsync(User recipient, BaseItem item)
        {
            var collection = await this.GetOrCreateCollectionAsync(recipient, item).ConfigureAwait(false);

            // Safe to call even when GetOrCreateCollectionAsync's own
            // CreateCollection call just added `item` as the seed:
            // BaseItem.AddCollection dedupes by linked-item id, so this is a
            // no-op in that case rather than a duplicate entry.
            await this.collectionManager.AddToCollection(collection.InternalId, new[] { item.InternalId }).ConfigureAwait(false);

            this.logger.Debug(
                "RecommendMe: added item {0} to collection {1} ({2}) for {3}.",
                item.InternalId,
                collection.InternalId,
                collection.Name,
                recipient.Name);
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

        /// <summary>
        /// True if <paramref name="itemId"/> is currently a member of
        /// <paramref name="recipient"/>'s recommendation collection, checked
        /// directly against the live collection (not against the JSON
        /// recommendation log, which can go stale - e.g. if the user
        /// manually removes the item from the collection outside the
        /// watched-cleanup flow). This is the sole source of truth for the
        /// "already recommended" submission-time gate; see
        /// RecommendationService.SendRecommendationAsync.
        /// </summary>
        public async Task<bool> IsItemInRecipientCollectionAsync(User recipient, long itemId)
        {
            var collectionId = await this.registryStore.GetCollectionIdAsync(recipient.InternalId).ConfigureAwait(false);
            if (!collectionId.HasValue)
            {
                this.logger.Debug(
                    "RecommendMe: {0} has no recommendation collection yet; item {1} cannot be a member.",
                    recipient.Name,
                    itemId);
                return false;
            }

            var matchingIds = this.libraryManager.GetInternalItemIds(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                CollectionIds = new[] { collectionId.Value },
                ItemIds = new[] { itemId }
            });

            var isMember = matchingIds.Length > 0;

            this.logger.Debug(
                "RecommendMe: membership check - item {0} in {1}'s collection {2}: {3}.",
                itemId,
                recipient.Name,
                collectionId.Value,
                isMember);

            return isMember;
        }
    }
}