using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace RecommendMe.Services
{
    /// <summary>
    /// Coalesces rapid, repeated collage-rebuild requests for the same
    /// collection into a single build. Membership changes tend to arrive in
    /// bursts (e.g. ClearWatchedRecommendationsTask removing several items
    /// from the same collection in one scheduled-task pass), and rebuilding
    /// the image on every single Add/Remove would mean discarding almost all
    /// of that work.
    ///
    /// Delay is fixed rather than adaptive: collage builds here read
    /// already-local library images (no network download, unlike
    /// SyncChannel's FolderCollageBuilder), so the operation itself is cheap.
    /// The delay exists purely to coalesce bursts, not to protect against a
    /// slow build - so a generous fixed value favors platform robustness
    /// (fewer concurrent image-processor/library-manager calls) over
    /// shaving latency off how quickly a collage reflects the latest change.
    /// </summary>
    internal class CollectionCollageCoordinator : IDisposable
    {
        private static readonly TimeSpan CoalesceDelay = TimeSpan.FromSeconds(5);

        private readonly CollectionCollageBuilder builder;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<long, CancellationTokenSource> pending = new ConcurrentDictionary<long, CancellationTokenSource>();
        private readonly ConcurrentDictionary<long, SemaphoreSlim> buildLocks = new ConcurrentDictionary<long, SemaphoreSlim>();
        private bool isDisposed;

        public CollectionCollageCoordinator(CollectionCollageBuilder builder, ILogger logger)
        {
            this.builder = builder;
            this.logger = logger;
        }

        /// <summary>
        /// Requests a collage rebuild for <paramref name="collectionId"/>.
        /// Fire-and-forget by design: called from CollectionSyncService's
        /// Add/RemoveItemAsync, which must not block on image processing.
        /// </summary>
        public void RequestRefresh(long collectionId)
        {
            if (this.isDisposed)
            {
                return;
            }

            // Cancel any still-pending (not yet started) build for this
            // collection and replace it - this is the coalescing step.
            var cts = new CancellationTokenSource();
            var previous = this.pending.AddOrUpdate(collectionId, cts, (_, old) =>
            {
                old.Cancel();
                old.Dispose();
                return cts;
            });

            _ = this.RunAfterDelayAsync(collectionId, cts);
        }

        private async Task RunAfterDelayAsync(long collectionId, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(CoalesceDelay, cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer request for the same collection.
                return;
            }
            finally
            {
                // Atomically remove our own token only if a newer request
                // hasn't already replaced it in the dictionary.
                ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long, CancellationTokenSource>>)this.pending)
                    .Remove(new System.Collections.Generic.KeyValuePair<long, CancellationTokenSource>(collectionId, cts));
            }

            if (this.isDisposed || cts.Token.IsCancellationRequested)
            {
                return;
            }

            var gate = this.buildLocks.GetOrAdd(collectionId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.isDisposed)
                {
                    return;
                }

                await this.builder.BuildAsync(collectionId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("CollectionCollage: unhandled error building collage for collection {0}", ex, collectionId);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>Cancels all pending (not-yet-fired) rebuilds. Called from Plugin.Dispose.</summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            foreach (var cts in this.pending.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            this.pending.Clear();
        }
    }
}