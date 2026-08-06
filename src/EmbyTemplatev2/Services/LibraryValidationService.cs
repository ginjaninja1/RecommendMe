using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbyTemplatev2.Services
{
    public class LibraryValidationService
    {
        private readonly ILibraryManager libraryManager;
        private readonly ILogger logger;

        public LibraryValidationService(ILibraryManager libraryManager, ILogger logger)
        {
            this.libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Audits server libraries matching active configuration paths, generating exactly one log per unique library.
        /// </summary>
        public void ValidateActiveConfiguredLibraries(List<string> enabledPaths)
        {
            if (enabledPaths == null || enabledPaths.Count == 0)
            {
                return;
            }

            try
            {
                var allVirtualFolders = this.libraryManager.GetVirtualFolders() ?? new List<VirtualFolderInfo>();

                // Tracks libraries we have already evaluated to enforce 1 log message per library maximum
                var evaluatedLibraryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var folder in allVirtualFolders)
                {
                    if (string.IsNullOrEmpty(folder.Name) || folder.LibraryOptions == null)
                    {
                        continue;
                    }

                    // Skip processing if this library has already been checked via another intersecting path
                    if (evaluatedLibraryNames.Contains(folder.Name))
                    {
                        continue;
                    }

                    // Determine if any path inside this library matches our active configurations
                    bool isLibraryActiveInConfig = folder.Locations?.Any(loc =>
                        enabledPaths.Any(activePath => IsUnderPath(activePath, loc) || IsUnderPath(loc, activePath))
                    ) ?? false;

                    if (isLibraryActiveInConfig)
                    {
                        // Add to our hash set immediately to prevent duplicates from other paths or multi-folder setups
                        evaluatedLibraryNames.Add(folder.Name);

                        if (!folder.LibraryOptions.DownloadImagesInAdvance)
                        {
                            this.logger.Info("Consider enabling 'Download images in advance' on library: {0}", folder.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error evaluating plugin validation metrics for active configurations", ex);
            }
        }

        private static bool IsUnderPath(string pathToCheck, string rootPath)
        {
            if (string.IsNullOrEmpty(pathToCheck) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            var normalizedRoot = rootPath.TrimEnd('\\', '/');
            return pathToCheck.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
