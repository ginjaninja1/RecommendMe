using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using System;
using System.IO;

namespace EmbyTemplatev2.Services
{
    public enum EvaluationResult
    {
        Copied,
        Skipped,
        Errored
    }

    /// <summary>
    /// Evaluates a single movie/show item and, if it has a primary ("poster") image
    /// but no folder image yet, copies the poster to folder.ext alongside it.
    /// </summary>
    public class PosterCopyService
    {
        // Windows-supported image extensions we recognize as a possible existing folder image.
        private static readonly string[] SupportedFolderImageExtensions = { ".jpg", ".png", ".gif" };

        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;

        public PosterCopyService(ILogger logger, IFileSystem fileSystem)
        {
            this.logger = logger;
            this.fileSystem = fileSystem;
        }

        /// <summary>Evaluates one item and copies its poster to folder.ext if applicable.</summary>
        /// <param name="item">The Movie or Series to evaluate.</param>
        /// <param name="typeLabel">Display label for logging, e.g. "Movie" or "Series".</param>
        /// <returns>An EvaluationResult indicating if the item was copied, skipped, or threw an error.</returns>
        public EvaluationResult EvaluateAndCopy(BaseItem item, string typeLabel)
        {
            var name = item.Name ?? item.Path ?? "(unknown)";
            var clientId = item.GetClientId();

            // ContainingFolderPath returns the item's own folder if it is folder-based (e.g. Series,
            // or a movie stored as "Movie Name/movie.mkv"), or the parent directory of the file
            // for single-file movies not stored in their own folder. Kept separate from the
            // resolved folderPath below so we can see in the log if/when the fallback kicks in.
            var containingFolderPathRaw = item.ContainingFolderPath;
            var folderPath = containingFolderPathRaw;

            if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(item.Path))
            {
                folderPath = this.fileSystem.DirectoryExists(item.Path)
                    ? item.Path
                    : Path.GetDirectoryName(item.Path);
            }

            // Source image: covers BOTH cases -
            //  (1) library saves artwork into the media folder -> ItemImageInfo.IsLocalFile = true,
            //      Path sits next to the video file.
            //  (2) library keeps only Emby's internal metadata cache -> IsLocalFile = false,
            //      Path sits under GetInternalMetadataPath() instead.
            // GetImagePath/GetImageInfo is meant to abstract over both, but we log everything
            // involved rather than assume that holds.
            var hasPrimaryImage = item.HasImage(ImageType.Primary, 0);
            ItemImageInfo primaryImageInfo = null;
            if (hasPrimaryImage)
            {
                primaryImageInfo = item.GetImageInfo(ImageType.Primary, 0);
            }

            var sourcePath = primaryImageInfo?.Path;
            var sourceIsLocalFile = primaryImageInfo?.IsLocalFile;
            var sourceExistsOnDisk = !string.IsNullOrEmpty(sourcePath) && this.fileSystem.FileExists(sourcePath);

            // IsLocalFile only tells us the image was downloaded to a real file rather than being a
            // remote URL reference - it's true whether that file lives next to the media or inside
            // Emby's internal metadata cache. We still want to know which of the two it is - purely
            // for visibility in the log - by comparing the image's directory to the item's own
            // resolved folder, mirroring the same check BaseItem itself uses internally
            // (FileSystem.ContainsSubPath(internalMetadataPath, itemImageInfo.Path)).
            // This does NOT gate whether we copy: the goal is a folder.ext in the movie folder
            // regardless of where the source poster currently lives.
            var sourceDirectory = string.IsNullOrEmpty(sourcePath) ? null : Path.GetDirectoryName(sourcePath);
            var sourceIsInItemFolder = !string.IsNullOrEmpty(sourceDirectory)
                && !string.IsNullOrEmpty(folderPath)
                && string.Equals(sourceDirectory.TrimEnd('\\', '/'), folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

            string sourceLocation;
            if (string.IsNullOrEmpty(sourcePath))
            {
                sourceLocation = "None";
            }
            else if (sourceIsInItemFolder)
            {
                sourceLocation = "MovieFolder";
            }
            else
            {
                sourceLocation = "Metadata";
            }

            string internalMetadataPath;
            try
            {
                internalMetadataPath = item.GetInternalMetadataPath();
            }
            catch (Exception ex)
            {
                internalMetadataPath = "(error reading GetInternalMetadataPath: " + ex.Message + ")";
            }

            var existingDestinationPath = string.IsNullOrEmpty(folderPath) ? null : this.FindExistingFolderImage(folderPath);

            this.logger.Debug(
                "{0} - {1} - [Id: {2}, Path: {3}, ContainingFolderPathRaw: {4}, ResolvedFolder: {5}, HasPrimaryImage: {6}, SourcePath: {7}, SourceLocation: {8}, SourceExistsOnDisk: {9}, InternalMetadataPath: {10}, ExistingDestination: {11}]",
                typeLabel,
                name,
                clientId,
                item.Path ?? "(null)",
                containingFolderPathRaw ?? "(null)",
                folderPath ?? "(null)",
                hasPrimaryImage,
                sourcePath ?? "(null)",
                sourceLocation,
                sourceExistsOnDisk,
                internalMetadataPath ?? "(null)",
                existingDestinationPath ?? "(none)");

            if (string.IsNullOrEmpty(folderPath))
            {
                this.logger.Warn("{0} - {1} - [Id: {2}] Could not determine a containing folder path, skipping. See Debug line above for full detail.", typeLabel, name, clientId);
                return EvaluationResult.Skipped;
            }

            // Nothing to do if there's no poster to copy at all, or a folder image already exists.
            // We copy regardless of SourceLocation (MovieFolder or Metadata) - the goal is a
            // folder.ext in the movie folder, wherever the source poster currently lives.
            if (!hasPrimaryImage || string.IsNullOrEmpty(sourcePath) || existingDestinationPath != null)
            {
                return EvaluationResult.Skipped;
            }

            if (!sourceExistsOnDisk)
            {
                this.logger.Warn(
                    "{0} - {1} - [Id: {2}] Emby reports a primary image at {3} (IsLocalFile={4}) but the file could not be found on disk.",
                    typeLabel,
                    name,
                    clientId,
                    sourcePath,
                    sourceIsLocalFile);
                return EvaluationResult.Errored;
            }

            var extension = Path.GetExtension(sourcePath);
            var destinationPath = Path.Combine(folderPath, "folder" + extension);

            try
            {
                this.fileSystem.CopyFile(sourcePath, destinationPath, false);
                this.logger.Info("{0} - {1} - Copied {2}", typeLabel, name, Path.GetFileName(destinationPath));
                return EvaluationResult.Copied;
            }
            catch (Exception ex)
            {
                this.logger.Warn(
                    "{0} - {1} - [Id: {2}] Failed to copy {3} to {4}: {5}",
                    typeLabel,
                    name,
                    clientId,
                    Path.GetFileName(sourcePath),
                    Path.GetFileName(destinationPath),
                    ex.Message);
                return EvaluationResult.Errored;
            }
        }

        private string FindExistingFolderImage(string folderPath)
        {
            foreach (var ext in SupportedFolderImageExtensions)
            {
                var candidate = Path.Combine(folderPath, "folder" + ext);
                if (this.fileSystem.FileExists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
