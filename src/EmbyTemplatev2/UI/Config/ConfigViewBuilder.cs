using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Entities;
using EmbyTemplatev2.Configuration;
using MediaBrowser.Model.Tasks;

namespace EmbyTemplatev2.UI.Config
{
    /// <summary>
    /// Builds the read-only, on-screen GenericItemList representation of the
    /// library/path filters. This is purely a view concern.
    ///
    /// BuildDisplayConfig always returns a NEW ConfigUI instance built from
    /// the persisted PluginConfiguration - it never hands back or mutates
    /// the persisted instance itself. ConfigUI is only ever used as
    /// ContentData; it is never passed to store.SetOptions.
    ///
    /// NOTE: currentFolders should already be filtered to relevant library
    /// types - see <see cref="RelevantLibraryTypes"/>.
    /// </summary>
    internal static class ConfigViewBuilder
    {
        public static ConfigUI BuildDisplayConfig(
            PluginConfiguration persistedConfig,
            IReadOnlyList<VirtualFolderInfo> currentFolders,
            ITaskManager taskManager)
        {
            // 1. Interrogate the task manager registry using your explicit task string Key
            var myTaskWorker = taskManager.ScheduledTasks
                .FirstOrDefault(t => string.Equals(t.ScheduledTask.Key, "EmbyTemplatev2Task", StringComparison.Ordinal));

            // 2. Build the router fragment location using the dynamically discovered worker ID
            string hyperlinkUrl = myTaskWorker != null
                ? $"/scheduledtask?id={myTaskWorker.Id}"
                : "/scheduledtasks";


            var display = new ConfigUI
            {
                EnablePlugin = persistedConfig.EnablePlugin,
                LibraryPaths = persistedConfig.LibraryPaths,

                // 3. Construct the list item element object hierarchy inside instantiation block
                ScheduledTaskLink = new GenericItemList
                {
                    new GenericListItem
                    {
                        PrimaryText = "Configure Scheduled Task",
                        SecondaryText = "Manage background execution rules and automation intervals",
                        Icon = IconNames.link,
                        Status = ItemStatus.Succeeded,
                        HyperLink = hyperlinkUrl,
                        HyperLinkTargetExternal = false // Directs Emby to leverage internal app routing
                    }
                }


            };

            display.LibraryList.Clear();

            var orderedFolders = currentFolders
                .OrderBy(x => x.Name)
                .ToList();

            foreach (var folder in orderedFolders)
            {
                display.LibraryList.Add(BuildLibraryItem(folder, persistedConfig.LibraryPaths));
            }

            return display;
        }

        private static GenericListItem BuildLibraryItem(
            VirtualFolderInfo folder,
            List<LibraryPathFilterItem> allStoredPaths)
        {
            var storedPathsForLibrary = allStoredPaths
                .Where(x => string.Equals(x.LibraryName, folder.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var locations = folder.Locations ?? Array.Empty<string>();

            var subItems = new GenericItemList();
            int enabledValidPaths = 0;
            int validPathCount = 0;

            // Currently valid, interactive paths.
            foreach (var location in locations)
            {
                validPathCount++;

                var stored = storedPathsForLibrary.FirstOrDefault(x =>
                    string.Equals(x.Path, location, StringComparison.OrdinalIgnoreCase));

                bool enabled = stored?.Enabled ?? false;

                if (enabled)
                {
                    enabledValidPaths++;
                }

                subItems.Add(new GenericListItem
                {
                    PrimaryText = location,
                    Icon = IconNames.folder,
                    IconMode = ItemListIconMode.SmallRegular,

                    Status = enabled
                        ? ItemStatus.Succeeded
                        : ItemStatus.Unavailable,

                    Toggle = new ToggleButtonItem("In Scope")
                    {
                        IsChecked = enabled,
                        CommandId = LibraryFilterCommands.BuildPathToggleCommandId(folder.Name, location)
                    }
                });
            }

            // Stale paths: previously saved, no longer part of this library.
            // Shown greyed out and non-interactive (no CommandId) rather than
            // pruned, so a saved preference survives if the path comes back.
            var stalePaths = storedPathsForLibrary
                .Where(x => !locations.Any(loc =>
                    string.Equals(loc, x.Path, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var stale in stalePaths)
            {
                subItems.Add(new GenericListItem
                {
                    PrimaryText = stale.Path,
                    SecondaryText = "No longer part of this library",
                    Icon = IconNames.folder,
                    IconMode = ItemListIconMode.SmallRegular,
                    Status = ItemStatus.Unavailable,

                    Toggle = new ToggleButtonItem("In Scope")
                    {
                        IsChecked = stale.Enabled

                        // No CommandId set on purpose: stale paths are not
                        // currently accessible, so the toggle is inert.
                    }
                });
            }

            bool libraryEnabled = validPathCount > 0 && enabledValidPaths > 0;

            string description;

            if (validPathCount == 0)
            {
                description = "No current paths for this library";
            }
            else if (!libraryEnabled)
            {
                description = "Disabled - Enable library and 1 or more paths to include";
            }
            else
            {
                description = $"{enabledValidPaths} of {validPathCount} paths monitored";
            }

            return new GenericListItem
            {
                PrimaryText = folder.Name,
                SecondaryText = description,

                Icon = IconNames.video_library,
                IconMode = ItemListIconMode.LargeRegular,

                Status = libraryEnabled
                    ? ItemStatus.Succeeded
                    : ItemStatus.Unavailable,

                Toggle = new ToggleButtonItem("In Scope")
                {
                    IsChecked = libraryEnabled,
                    CommandId = LibraryFilterCommands.BuildLibraryToggleCommandId(folder.Name)
                },

                SubItems = subItems
            };
        }
    }
}