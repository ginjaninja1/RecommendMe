using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Builds and applies the Primary image for a single recommendation
    /// Collection, collaged from its 4 most-recently-added members. Ported
    /// from SyncChannel's FolderCollageBuilder, simplified: members here are
    /// already local library items (no remote poster download needed), so
    /// this reads each item's existing ItemImageInfo.Path directly.
    ///
    /// Call sites should not call this directly on every membership change -
    /// see CollectionCollageCoordinator, which coalesces bursts of changes
    /// before invoking this.
    /// </summary>
    internal class CollectionCollageBuilder
    {
        private readonly ILibraryManager libraryManager;
        private readonly IImageProcessor imageProcessor;
        private readonly IApplicationPaths appPaths;
        private readonly CollectionCollageStore collageStore;
        private readonly ILogger logger;

        public CollectionCollageBuilder(
            ILibraryManager libraryManager,
            IImageProcessor imageProcessor,
            IApplicationPaths appPaths,
            CollectionCollageStore collageStore,
            ILogger logger)
        {
            this.libraryManager = libraryManager;
            this.imageProcessor = imageProcessor;
            this.appPaths = appPaths;
            this.collageStore = collageStore;
            this.logger = logger;
        }

        public async Task BuildAsync(long collectionId, CancellationToken cancellationToken)
        {
            var collection = this.libraryManager.GetItemById(collectionId) as BoxSet;
            if (collection == null)
            {
                this.logger.Debug("CollectionCollage: skipped {0} - not a BoxSet (deleted?).", collectionId);
                return;
            }

            var recentIds = await this.collageStore.GetRecentItemIdsAsync(collectionId).ConfigureAwait(false);
            if (recentIds.Count == 0)
            {
                this.logger.Debug("CollectionCollage: skipped '{0}' - no tracked members.", collection.Name);
                return;
            }

            // Cross-check against live membership: our tracked history can
            // drift from reality if a user edits the collection manually
            // outside this plugin's Add/Remove flow.
            var liveMemberIds = new System.Collections.Generic.HashSet<long>(
                this.libraryManager.GetInternalItemIds(new InternalItemsQuery
                {
                    CollectionIds = new[] { collectionId }
                }));

            var top4Ids = recentIds.Where(liveMemberIds.Contains).Take(4).ToList();
            if (top4Ids.Count == 0)
            {
                this.logger.Debug("CollectionCollage: skipped '{0}' - tracked members no longer in collection.", collection.Name);
                return;
            }

            var lastBuiltIds = await this.collageStore.GetLastBuiltItemIdsAsync(collectionId).ConfigureAwait(false);
            if (collection.HasImage(ImageType.Primary, 0) && top4Ids.SequenceEqual(lastBuiltIds))
            {
                this.logger.Debug("CollectionCollage: skipped '{0}' - top-4 set unchanged since last build.", collection.Name);
                return;
            }

            var localPaths = top4Ids
                .Select(id => this.libraryManager.GetItemById(id))
                .Where(item => item != null)
                .Select(item => item.GetImagePath(ImageType.Primary, 0))
                .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
                .ToArray();

            if (localPaths.Length == 0)
            {
                this.logger.Warn("CollectionCollage: skipped '{0}' - none of the top-{1} member(s) have a Primary image.", collection.Name, top4Ids.Count);
                return;
            }

            var outputDir = RecommendMeDataPaths.CollectionCollageImagesDirectory(this.appPaths);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var outputPath = Path.Combine(outputDir, collectionId + ".jpg");

            var options = new ImageCollageOptions
            {
                Images = localPaths.Select(p => new ItemImageInfo { Path = p, Type = ImageType.Primary }).ToArray(),
                OutputPath = outputPath,
                Width = 400,
                Height = 600
            };

            try
            {
                await this.imageProcessor.CreateImageCollage(options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("CollectionCollage: build failed for '{0}'", ex, collection.Name);
                return;
            }

            if (!File.Exists(outputPath))
            {
                this.logger.Warn("CollectionCollage: build reported success but no file exists for '{0}' at {1}.", collection.Name, outputPath);
                return;
            }

            try
            {
                var imageSize = this.imageProcessor.GetImageSize(outputPath);

                collection.SetImage(new ItemImageInfo
                {
                    Path = outputPath,
                    Type = ImageType.Primary,
                    DateModified = DateTimeOffset.UtcNow,
                    Width = (int)imageSize.Width,
                    Height = (int)imageSize.Height
                }, 0);

                this.libraryManager.UpdateImages(collection);

                this.logger.Info("CollectionCollage: applied to '{0}' ({1} poster(s)).", collection.Name, localPaths.Length);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("CollectionCollage: failed to attach image to '{0}'", ex, collection.Name);
                return;
            }

            await this.collageStore.SetLastBuiltItemIdsAsync(collectionId, top4Ids).ConfigureAwait(false);
        }
    }
}