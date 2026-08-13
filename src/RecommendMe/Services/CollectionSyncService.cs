using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using System;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
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
    internal class CollectionSyncService
    {
        private readonly ICollectionManager collectionManager;
        private readonly ILibraryManager libraryManager;
        private readonly IUserManager userManager;
        private readonly CollectionRegistryStore registryStore;
        private readonly AdminSettingsStore adminSettingsStore;
        private readonly PendingCollectionAddCache pendingCollectionAddCache;
        private readonly ILogger logger;
        private readonly SemaphoreSlim collectionGate = new SemaphoreSlim(1, 1);

        public CollectionSyncService(
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            CollectionRegistryStore registryStore,
            AdminSettingsStore adminSettingsStore,
            PendingCollectionAddCache pendingCollectionAddCache,
            ILogger logger)
        {
            this.collectionManager = collectionManager;
            this.libraryManager = libraryManager;
            this.userManager = userManager;
            this.registryStore = registryStore;
            this.adminSettingsStore = adminSettingsStore;
            this.pendingCollectionAddCache = pendingCollectionAddCache;
            this.logger = logger;
        }

        public static string CollectionNameFor(User user, string prefix, string suffix) =>
            (prefix ?? string.Empty) + user.Name + (suffix ?? string.Empty);

        private static string PublicId(BoxSet collection) => collection.Id.ToString("N");

        private static bool RegistryIdentityMatches(CollectionRegistryEntry entry, BoxSet collection) =>
            string.Equals(entry.EmbyCollectionId, PublicId(collection), StringComparison.OrdinalIgnoreCase);

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
            await this.collectionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await this.GetOrCreateCollectionCoreAsync(recipient, seedItem).ConfigureAwait(false);
            }
            finally
            {
                this.collectionGate.Release();
            }
        }

        private async Task<BoxSet> GetOrCreateCollectionCoreAsync(User recipient, BaseItem seedItem)
        {
            var registryEntry = await this.registryStore.GetAsync(recipient.InternalId).ConfigureAwait(false);
            if (registryEntry != null)
            {
                var existingItem = this.libraryManager.GetItemById(registryEntry.CollectionId) as BoxSet;
                if (existingItem != null && RegistryIdentityMatches(registryEntry, existingItem))
                {
                    await this.registryStore.RegisterAsync(
                        recipient.InternalId,
                        recipient.Name,
                        existingItem.InternalId,
                        existingItem.Name,
                        PublicId(existingItem)).ConfigureAwait(false);
                    return existingItem;
                }

                this.logger.Warn(
                    "Registered collection id {0} for user {1} no longer resolves to a BoxSet; recreating.",
                    registryEntry.CollectionId,
                    recipient.Name);
            }

            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);
            var name = CollectionNameFor(recipient, settings.RecommendationCollectionPrefix, settings.RecommendationCollectionSuffix);

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
                    $"CreateCollection returned no BoxSet for '{name}' (seed item {seedItem.InternalId}).");
            }

            await this.registryStore.RegisterAsync(
                recipient.InternalId,
                recipient.Name,
                created.InternalId,
                created.Name,
                PublicId(created)).ConfigureAwait(false);

            return created;
        }

        /// <summary>Renames only registered collections that already exist; it never creates missing collections.</summary>
        public async Task<CollectionRenameResult> RenameInstantiatedCollectionsAsync(string prefix, string suffix)
        {
            await this.collectionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await this.RenameInstantiatedCollectionsCoreAsync(prefix, suffix).ConfigureAwait(false);
            }
            finally
            {
                this.collectionGate.Release();
            }
        }

        private async Task<CollectionRenameResult> RenameInstantiatedCollectionsCoreAsync(string prefix, string suffix)
        {
            var result = new CollectionRenameResult();
            var entries = await this.registryStore.GetAllAsync().ConfigureAwait(false);
            var users = this.userManager.GetUserList(new UserQuery()).ToDictionary(user => user.InternalId);

            foreach (var entry in entries)
            {
                if (!users.TryGetValue(entry.UserId, out var user))
                {
                    result.Skipped++;
                    continue;
                }

                var collection = this.libraryManager.GetItemById(entry.CollectionId) as BoxSet;
                if (collection == null || !RegistryIdentityMatches(entry, collection))
                {
                    result.Skipped++;
                    this.logger.Warn("Skipped renaming registered collection {0}; its identity no longer matches.", entry.CollectionId);
                    continue;
                }

                var newName = CollectionNameFor(user, prefix, suffix);
                if (!string.Equals(collection.Name, newName, StringComparison.Ordinal))
                {
                    collection.Name = newName;
                    this.libraryManager.UpdateItem(collection, collection.GetParent(), ItemUpdateType.MetadataEdit, null);
                    result.Renamed++;
                }

                await this.registryStore.RegisterAsync(
                    user.InternalId,
                    user.Name,
                    collection.InternalId,
                    collection.Name,
                    PublicId(collection)).ConfigureAwait(false);
            }

            return result;
        }

        public async Task AddItemAsync(User recipient, BaseItem item)
        {
            var collection = await this.GetOrCreateCollectionAsync(recipient, item).ConfigureAwait(false);

            // Marked before the call (not after) so the listener can never
            // observe the resulting event before the mark exists.
            this.pendingCollectionAddCache.MarkExpected(collection.InternalId, item.InternalId);

            // Safe to call even when GetOrCreateCollectionAsync's own
            // CreateCollection call just added `item` as the seed:
            // BaseItem.AddCollection dedupes by linked-item id, so this is a
            // no-op in that case rather than a duplicate entry.
            await this.collectionManager.AddToCollection(collection.InternalId, new[] { item.InternalId }).ConfigureAwait(false);

            this.logger.Debug(
                "Added item {0} to collection {1} ({2}) for {3}.",
                item.InternalId,
                collection.InternalId,
                collection.Name,
                recipient.Name);
        }

        public async Task<bool> RemoveItemAsync(User recipient, long itemId)
        {
            var entry = await this.registryStore.GetAsync(recipient.InternalId).ConfigureAwait(false);
            if (entry == null)
            {
                this.logger.Warn(
                    "Cannot remove item {0} for {1} ({2}): no registered recommendation collection.",
                    itemId,
                    recipient.Name,
                    recipient.InternalId);
                return false;
            }

            var collection = this.libraryManager.GetItemById(entry.CollectionId) as BoxSet;
            if (collection == null || !RegistryIdentityMatches(entry, collection))
            {
                this.logger.Warn(
                    "Cannot remove item {0} for {1} ({2}): registered collection {3} identity does not resolve.",
                    itemId,
                    recipient.Name,
                    recipient.InternalId,
                    entry.CollectionId);
                return false;
            }

            this.collectionManager.RemoveFromCollection(collection, new[] { itemId });
            return true;
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
            var entry = await this.registryStore.GetAsync(recipient.InternalId).ConfigureAwait(false);
            if (entry == null)
            {
                this.logger.Debug(
                    "{0} has no recommendation collection yet; item {1} cannot be a member.",
                    recipient.Name,
                    itemId);
                return false;
            }

            var collection = this.libraryManager.GetItemById(entry.CollectionId) as BoxSet;
            if (collection == null || !RegistryIdentityMatches(entry, collection))
            {
                this.logger.Warn("Collection registry identity mismatch for user {0}.", recipient.Name);
                return false;
            }

            var matchingIds = this.libraryManager.GetInternalItemIds(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                CollectionIds = new[] { entry.CollectionId },
                ItemIds = new[] { itemId }
            });

            var isMember = matchingIds.Length > 0;

            this.logger.Debug(
                "Membership check - item {0} in {1}'s collection {2}: {3}.",
                itemId,
                recipient.Name,
                entry.CollectionId,
                isMember);

            return isMember;
        }
    }

}
