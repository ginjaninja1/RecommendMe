using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace EmbyTemplatev2.UI.Config
{
    /// <summary>
    /// Poster To Folder only makes sense for libraries that hold movies or
    /// TV shows. Playlists, Music, Books, Home Videos, etc. should never be
    /// offered in the config UI, never have paths added to LibraryPaths, and
    /// never be considered by the scheduled task.
    ///
    /// Used as the single choke point wherever the plugin looks at
    /// <c>ILibraryManager.GetVirtualFolders()</c>, so the UI and the
    /// scheduled task can never drift out of sync on what counts as
    /// "relevant".
    /// </summary>
    internal static class RelevantLibraryTypes
    {
        private static readonly string[] AllowedCollectionTypes =
        {
            "movies",
            "tvshows"
        };

        public static bool IsRelevant(VirtualFolderInfo folder)
        {
            return AllowedCollectionTypes.Any(allowed =>
                string.Equals(folder.CollectionType, allowed, StringComparison.OrdinalIgnoreCase));
        }

        public static IReadOnlyList<VirtualFolderInfo> Filter(IEnumerable<VirtualFolderInfo> folders)
        {
            return folders.Where(IsRelevant).ToList();
        }
    }
}