using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Model.Logging;
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
    /// calls, admin manual edits via the Emby UI, and anything else - so this
    /// listener is the single, self-contained source of truth for collage
    /// recency tracking. CollectionSyncService itself has no knowledge of
    /// collages at all.
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
        private readonly ILogger logger;
        private readonly object lifecycleLock = new object();
        private bool isSubscribed;
        private bool isDisposed;

        public CollectionCollageEventListener(
            ICollectionManager collectionManager,
            CollectionRegistryStore registryStore,
            CollectionCollageStore collageStore,
            CollectionCollageCoordinator collageCoordinator,
            ILogger logger)
        {
            this.collectionManager = collectionManager;
            this.registryStore = registryStore;
            this.collageStore = collageStore;
            this.collageCoordinator = collageCoordinator;
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
                if (!await this.IsManagedCollectionAsync(collectionId).ConfigureAwait(false))
                {
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                foreach (var itemId in itemIds)
                {
                    if (isAdd)
                    {
                        await this.collageStore.RecordItemAddedAsync(collectionId, itemId, now).ConfigureAwait(false);
                    }
                    else
                    {
                        await this.collageStore.RecordItemRemovedAsync(collectionId, itemId).ConfigureAwait(false);
                    }
                }

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

        private async Task<bool> IsManagedCollectionAsync(long collectionId)
        {
            var entries = await this.registryStore.GetAllAsync().ConfigureAwait(false);
            return entries.Any(entry => entry.CollectionId == collectionId);
        }
    }
}