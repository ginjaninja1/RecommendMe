using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Keeps CollectionCollageStore's add-order tracking in sync with a
    /// recommendation collection's actual membership by subscribing directly
    /// to ICollectionManager.ItemsAddedToCollection/ItemsRemovedFromCollection
    /// (confirmed via ILSpy against MediaBrowser.Controller 4.10.0.22 -
    /// CollectionModifiedEventArgs.Collection is the BoxSet directly,
    /// .ItemsChanged is IList&lt;long&gt; InternalIds directly).
    ///
    /// These events fire for every membership change regardless of source -
    /// including CollectionSyncService's own AddToCollection/RemoveFromCollection
    /// calls, admin manual edits via the Emby UI, and anything else. This
    /// listener uses PendingCollectionAddCache to tell those two cases apart
    /// for adds: an add this plugin itself just made is expected (marked by
    /// CollectionSyncService.AddItemAsync just before it happens); anything
    /// else is an out-of-plugin add, which this listener records into the
    /// recommendation history as a System-sent recommendation and notifies
    /// the recipient about, in addition to feeding it into collage recency
    /// tracking the same as any other add.
    ///
    /// Only reacts to collections registered in CollectionRegistryStore (this
    /// plugin's own "_Recommended_{username}" collections) - unrelated BoxSets
    /// on the server are ignored.
    /// </summary>
    internal class CollectionCollageEventListener : IDisposable
    {
        private readonly ICollectionManager collectionManager;
        private readonly CollectionRegistryStore registryStore;
        private readonly CollectionCollageStore collageStore;
        private readonly CollectionCollageCoordinator collageCoordinator;
        private readonly PendingCollectionAddCache pendingCollectionAddCache;
        private readonly RecommendationStore recommendationStore;
        private readonly NotificationService notificationService;
        private readonly IUserManager userManager;
        private readonly ILibraryManager libraryManager;
        private readonly ILogger logger;
        private readonly object lifecycleLock = new object();
        private bool isSubscribed;
        private bool isDisposed;

        public CollectionCollageEventListener(
            ICollectionManager collectionManager,
            CollectionRegistryStore registryStore,
            CollectionCollageStore collageStore,
            CollectionCollageCoordinator collageCoordinator,
            PendingCollectionAddCache pendingCollectionAddCache,
            RecommendationStore recommendationStore,
            NotificationService notificationService,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogger logger)
        {
            this.collectionManager = collectionManager;
            this.registryStore = registryStore;
            this.collageStore = collageStore;
            this.collageCoordinator = collageCoordinator;
            this.pendingCollectionAddCache = pendingCollectionAddCache;
            this.recommendationStore = recommendationStore;
            this.notificationService = notificationService;
            this.userManager = userManager;
            this.libraryManager = libraryManager;
            this.logger = logger;
        }

        public void Start()
        {
            lock (this.lifecycleLock)
            {
                if (this.isDisposed || this.isSubscribed)
                {
                    return;
                }

                this.collectionManager.ItemsAddedToCollection += this.OnItemsAddedToCollection;
                this.collectionManager.ItemsRemovedFromCollection += this.OnItemsRemovedFromCollection;
                this.isSubscribed = true;
            }
        }

        public void Dispose()
        {
            lock (this.lifecycleLock)
            {
                if (this.isDisposed)
                {
                    return;
                }

                this.isDisposed = true;

                if (this.isSubscribed)
                {
                    this.collectionManager.ItemsAddedToCollection -= this.OnItemsAddedToCollection;
                    this.collectionManager.ItemsRemovedFromCollection -= this.OnItemsRemovedFromCollection;
                    this.isSubscribed = false;
                }
            }
        }

        private void OnItemsAddedToCollection(object sender, CollectionModifiedEventArgs e) =>
            this.Handle(e, isAdd: true);

        private void OnItemsRemovedFromCollection(object sender, CollectionModifiedEventArgs e) =>
            this.Handle(e, isAdd: false);

        private void Handle(CollectionModifiedEventArgs e, bool isAdd)
        {
            var collection = e?.Collection;
            if (collection == null || e.ItemsChanged == null || e.ItemsChanged.Count == 0)
            {
                return;
            }

            lock (this.lifecycleLock)
            {
                if (this.isDisposed)
                {
                    return;
                }
            }

            var itemIds = e.ItemsChanged.ToArray();

            // Emby's event handlers are synchronous - do not block the
            // collection-manager's own event dispatch on our JSON I/O.
            _ = Task.Run(() => this.HandleAsync(collection.InternalId, itemIds, isAdd));
        }

        private async Task HandleAsync(long collectionId, long[] itemIds, bool isAdd)
        {
            try
            {
                var registryEntry = await this.GetManagedEntryAsync(collectionId).ConfigureAwait(false);
                if (registryEntry == null)
                {
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                foreach (var itemId in itemIds)
                {
                    if (isAdd)
                    {
                        await this.collageStore.RecordItemAddedAsync(collectionId, itemId, now).ConfigureAwait(false);

                        if (!this.pendingCollectionAddCache.TryConsumeExpected(collectionId, itemId))
                        {
                            await this.HandleOutOfPluginAddAsync(registryEntry, itemId).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await this.collageStore.RecordItemRemovedAsync(collectionId, itemId).ConfigureAwait(false);
                    }
                }

                // Out-of-plugin adds feed the collage the same as any other
                // add - the request goes out regardless of which branch above ran.
                this.collageCoordinator.RequestRefresh(collectionId);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException(
                    "CollectionCollage: error handling {0} for collection {1}",
                    ex,
                    isAdd ? "ItemsAddedToCollection" : "ItemsRemovedFromCollection",
                    collectionId);
            }
        }

        /// <summary>
        /// An item showed up in a managed "_Recommended_{username}"
        /// collection without CollectionSyncService.AddItemAsync having put
        /// it there. Recorded into recommendation history with sender
        /// RecommendationRecord.SystemSenderName (there is no real sending
        /// user), and notified immediately/queued exactly like a normal
        /// recommendation - bypassing PermissionService and recipient
        /// preferences entirely, since this path is admin-forced by
        /// definition.
        /// </summary>
        private async Task HandleOutOfPluginAddAsync(CollectionRegistryEntry registryEntry, long itemId)
        {
            var recipient = this.userManager.GetUserList(new UserQuery())
                .FirstOrDefault(user => user.InternalId == registryEntry.UserId);
            if (recipient == null)
            {
                this.logger.Warn(
                    "CollectionCollage: out-of-plugin add of item {0} to collection {1} ignored - recipient user {2} not found.",
                    itemId,
                    registryEntry.CollectionId,
                    registryEntry.UserId);
                return;
            }

            var item = this.libraryManager.GetItemById(itemId);
            if (item == null)
            {
                this.logger.Warn(
                    "CollectionCollage: out-of-plugin add of item {0} to {1}'s collection ignored - item not found.",
                    itemId,
                    recipient.Name);
                return;
            }

            var mediaType = item.GetType().Name;

            this.logger.Info(
                "CollectionCollage: detected out-of-plugin add - item {0} '{1}' ({2}) added to {3}'s recommendation collection outside the plugin. Recording as {4} recommendation and notifying.",
                itemId,
                item.Name,
                mediaType,
                recipient.Name,
                RecommendationRecord.SystemSenderName);

            var record = new RecommendationRecord
            {
                ItemId = itemId,
                ItemName = item.Name,
                MediaType = mediaType,
                SentByUserId = 0,
                SentByUserName = RecommendationRecord.SystemSenderName,
                SentToUserId = recipient.InternalId,
                SentToUserName = recipient.Name,
                IsPrivate = false
            };

            await this.recommendationStore.AddAsync(record).ConfigureAwait(false);

            await this.notificationService
                .NotifyOutOfPluginAdditionAsync(recipient, item.Name, mediaType)
                .ConfigureAwait(false);

            this.logger.Debug(
                "CollectionCollage: out-of-plugin recommendation {0} recorded for item {1} -> {2}.",
                record.RecommendationId,
                itemId,
                recipient.Name);
        }

        private async Task<CollectionRegistryEntry> GetManagedEntryAsync(long collectionId)
        {
            var entries = await this.registryStore.GetAllAsync().ConfigureAwait(false);
            return entries.FirstOrDefault(entry => entry.CollectionId == collectionId);
        }
    }
}