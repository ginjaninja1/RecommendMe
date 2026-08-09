using System;
using System.Collections.Concurrent;

namespace RecommendMe.Services
{
    /// <summary>
    /// Short-lived in-memory record of collection Add operations this plugin
    /// itself just performed (via CollectionSyncService.AddItemAsync), so
    /// CollectionCollageEventListener can tell an in-plugin add apart from
    /// one made outside the plugin (e.g. an admin manually adding an item to
    /// a "_Recommended_{username}" collection via the Emby UI).
    ///
    /// ICollectionManager's ItemsAddedToCollection event carries no origin
    /// information (confirmed via ILSpy - CollectionModifiedEventArgs is
    /// just Collection + ItemsChanged), so self-correlation is the only
    /// option: mark the (collectionId, itemId) pair as "expected" right
    /// before calling ICollectionManager.AddToCollection, then have the
    /// listener consume that mark when its event fires.
    ///
    /// Deliberately in-memory and TTL-based rather than persisted: this only
    /// needs to bridge the gap between our own AddToCollection call and the
    /// event reaching the listener (which itself dispatches via Task.Run),
    /// not survive a restart. A generous TTL favors correctness (never
    /// misclassifying a genuine in-plugin add as out-of-plugin) over
    /// tightly bounding the window.
    /// </summary>
    internal class PendingCollectionAddCache
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

        private readonly ConcurrentDictionary<(long CollectionId, long ItemId), DateTimeOffset> expectedAdds =
            new ConcurrentDictionary<(long, long), DateTimeOffset>();

        /// <summary>Marks an add this plugin is about to make as expected.</summary>
        public void MarkExpected(long collectionId, long itemId)
        {
            this.expectedAdds[(collectionId, itemId)] = DateTimeOffset.UtcNow.Add(Ttl);
        }

        /// <summary>
        /// True (and consumes the mark) if this (collectionId, itemId) add
        /// was made by this plugin and hasn't expired; false otherwise,
        /// meaning the add came from outside the plugin.
        /// </summary>
        public bool TryConsumeExpected(long collectionId, long itemId)
        {
            var key = (collectionId, itemId);
            if (!this.expectedAdds.TryRemove(key, out var expiresAt))
            {
                return false;
            }

            return DateTimeOffset.UtcNow <= expiresAt;
        }
    }
}