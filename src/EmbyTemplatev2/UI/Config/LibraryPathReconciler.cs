using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;
using EmbyTemplatev2.Configuration;

namespace EmbyTemplatev2.UI.Config
{
    /// <summary>
    /// Pure domain logic for reconciling the persisted library/path filter
    /// list against Emby's current library layout. No UI/visual concerns
    /// live here - see <see cref="ConfigViewBuilder"/> for that.
    ///
    /// Rule: paths/libraries that disappear from Emby are NEVER pruned from
    /// this list (they may come back later). Only currently-valid paths can
    /// be toggled from the UI - see IsPathCurrentlyValid.
    ///
    /// NOTE: callers should pass in a list already filtered down to relevant
    /// library types - see <see cref="RelevantLibraryTypes"/>.
    /// </summary>
    internal static class LibraryPathReconciler
    {
        /// <summary>
        /// Adds any newly discovered library paths to the persisted config.
        /// Never removes existing entries.
        /// </summary>
        public static void EnsureDiscoveredPaths(
            PluginConfiguration config,
            IReadOnlyList<VirtualFolderInfo> currentFolders)
        {
            if (config.LibraryPaths == null)
            {
                config.LibraryPaths = new List<LibraryPathFilterItem>();
            }

            foreach (var folder in currentFolders)
            {
                var locations = folder.Locations ?? Array.Empty<string>();

                foreach (var location in locations)
                {
                    var exists = config.LibraryPaths.Any(x =>
                        string.Equals(x.Path, location, StringComparison.OrdinalIgnoreCase));

                    if (!exists)
                    {
                        config.LibraryPaths.Add(new LibraryPathFilterItem
                        {
                            LibraryName = folder.Name,
                            Path = location,
                            Enabled = false
                        });
                    }
                }
            }
        }

        /// <summary>
        /// True when the named library still exists (amongst the relevant,
        /// current folders passed in) and the path is still one of its
        /// current locations. False for stale/removed/irrelevant entries -
        /// these are not accessible from the UI even if still on disk.
        /// </summary>
        public static bool IsPathCurrentlyValid(
            IReadOnlyList<VirtualFolderInfo> currentFolders,
            string libraryName,
            string path)
        {
            var folder = currentFolders.FirstOrDefault(x =>
                string.Equals(x.Name, libraryName, StringComparison.OrdinalIgnoreCase));

            if (folder == null)
            {
                return false;
            }

            var locations = folder.Locations ?? Array.Empty<string>();

            return locations.Any(loc =>
                string.Equals(loc, path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Toggles every currently-valid path for a library as a group. Stale
        /// paths under the same library name are left untouched, since they
        /// are not currently accessible.
        /// Returns true if anything changed (caller should persist).
        /// </summary>
        public static bool ToggleLibrary(
            PluginConfiguration config,
            IReadOnlyList<VirtualFolderInfo> currentFolders,
            string libraryName)
        {
            var validPaths = config.LibraryPaths
                .Where(x =>
                    string.Equals(x.LibraryName, libraryName, StringComparison.OrdinalIgnoreCase) &&
                    IsPathCurrentlyValid(currentFolders, libraryName, x.Path))
                .ToList();

            if (validPaths.Count == 0)
            {
                return false;
            }

            bool newState = !validPaths.Any(x => x.Enabled);

            foreach (var path in validPaths)
            {
                path.Enabled = newState;
            }

            return true;
        }

        /// <summary>
        /// Toggles a single path. No-op (returns false) if the path is stale -
        /// only currently valid paths can be toggled from the UI.
        /// </summary>
        public static bool TogglePath(
            PluginConfiguration config,
            IReadOnlyList<VirtualFolderInfo> currentFolders,
            string libraryName,
            string path)
        {
            if (!IsPathCurrentlyValid(currentFolders, libraryName, path))
            {
                return false;
            }

            var entry = config.LibraryPaths.FirstOrDefault(x =>
                string.Equals(x.LibraryName, libraryName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                return false;
            }

            entry.Enabled = !entry.Enabled;
            return true;
        }
    }
}