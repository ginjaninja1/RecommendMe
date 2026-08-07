using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.TV;
using RecommendMe.Models;
using RecommendMe.Services;

namespace RecommendMe.UI.Recommend
{
    /// <summary>
    /// Builds the dynamic (non-persisted) parts of RecommendUI: the target
    /// user dropdown source and the search results list. Pure view-building -
    /// no persistence, no permission enforcement (that happens for real at
    /// send-time in RecommendationService; this is convenience filtering
    /// only, per <see cref="RecommendUI"/>'s remarks).
    /// </summary>
    internal static class RecommendViewBuilder
    {
        public static List<EditorSelectOption> BuildMediaTypeChoices(IEnumerable<string> allowedMediaTypes)
        {
            return (allowedMediaTypes ?? Enumerable.Empty<string>())
                .Distinct(System.StringComparer.Ordinal)
                .Select(type => new EditorSelectOption(type, GetMediaTypeDisplayName(type)))
                .ToList();
        }

        public static void SetSearchActionMessage(RecommendUI ui, int resultCount)
        {
            if (ui.SearchAction == null || ui.SearchAction.Count == 0)
            {
                ui.SearchAction = new RecommendUI().SearchAction;
            }

            if (resultCount > 0)
            {
                ui.SearchAction[0].SecondaryText = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(ui.SearchTerm))
            {
                ui.SearchAction[0].SecondaryText = "Enter a title to search your library.";
            }
            else if (string.IsNullOrWhiteSpace(ui.SelectedMediaTypes))
            {
                ui.SearchAction[0].SecondaryText = "Select at least one media type to search.";
            }
            else
            {
                ui.SearchAction[0].SecondaryText = $"No results found for ‘{ui.SearchTerm.Trim()}’. Try another title or broaden the media-type filter.";
            }
        }

        private static string GetMediaTypeDisplayName(string type)
        {
            switch (type)
            {
                case RecommendableMediaTypes.Movie: return "Movies";
                case RecommendableMediaTypes.Person: return "People";
                case RecommendableMediaTypes.Series: return "TV series";
                case RecommendableMediaTypes.Season: return "TV seasons";
                case RecommendableMediaTypes.Episode: return "TV episodes";
                case RecommendableMediaTypes.MusicArtist: return "Music artists";
                case RecommendableMediaTypes.MusicAlbum: return "Music albums";
                case RecommendableMediaTypes.Song: return "Songs";
                case RecommendableMediaTypes.BoxSet: return "Collections";
                default: return type;
            }
        }

        public static async Task<List<EditorSelectOption>> BuildTargetUserChoicesAsync(User currentUser)
        {
            var plugin = Plugin.Instance;

            // Value is always the raw, unmodified username - ResolveTargetUser
            // matches on this. Only the display Name gets the "(yourself)"
            // decoration, so there's no fragile string-suffix matching.
            var choices = new List<EditorSelectOption>
            {
                new EditorSelectOption(currentUser.Name, currentUser.Name + " (yourself)")
            };

            var allUsers = plugin.GetAllUsers();
            foreach (var user in allUsers)
            {
                await plugin.PermissionService.EnsureUserAccessEntryAsync(user).ConfigureAwait(false);
            }

            var currentUserEntry = await plugin.PermissionService.EnsureUserAccessEntryAsync(currentUser).ConfigureAwait(false);
            var settings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            if (currentUserEntry.AccessSuspended)
            {
                // Suspended users can still recommend to themselves, but not to anyone else.
                return choices;
            }

            foreach (var candidate in allUsers)
            {
                if (candidate.InternalId == currentUser.InternalId)
                {
                    continue;
                }

                var candidateEntry = settings.UserAccess.First(u => u.UserId == candidate.InternalId);
                if (candidateEntry.AccessSuspended)
                {
                    continue;
                }

                if (PermissionService.IsTargetAllowed(currentUserEntry, candidate.InternalId, settings))
                {
                    choices.Add(new EditorSelectOption(candidate.Name, candidate.Name));
                }
            }

            return choices;
        }

        public static GenericItemList BuildSearchResults(IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> items)
        {
            var list = new GenericItemList();

            foreach (var item in items)
            {
                list.Add(BuildMediaItem(item));
            }

            return list;
        }

        public static GenericListItem BuildMediaItem(BaseItem item)
        {
            var result = new GenericListItem
            {
                PrimaryText = FormatTitle(item),
                SecondaryText = string.Empty,
                Icon = GetIcon(item.GetType().Name),
                IconMode = ItemListIconMode.SmallRegular,
                Status = ItemStatus.Succeeded,
                Button2 = new ButtonItem("Recommend") { CommandId = RecommendCommands.BuildSendCommandId(item.InternalId) }
            };

            if (CanExpand(item.GetType().Name))
            {
                result.Button1 = new ButtonItem("Info") { CommandId = RecommendCommands.BuildExpandCommandId(item.InternalId) };
            }

            return result;
        }

        public static bool TryToggleChildren(GenericItemList list, long parentId, System.Func<IReadOnlyList<BaseItem>> getChildren)
        {
            foreach (var row in list)
            {
                if (TryToggleChildren(row, parentId, getChildren)) return true;
            }
            return false;
        }

        private static bool TryToggleChildren(GenericListItem row, long parentId, System.Func<IReadOnlyList<BaseItem>> getChildren)
        {
            if (row.Button2?.CommandId == RecommendCommands.BuildSendCommandId(parentId))
            {
                row.SubItems = row.SubItems == null
                    ? getChildren().Select(BuildMediaItem).ToList()
                    : null;
                return true;
            }
            if (row.SubItems == null) return false;
            foreach (var child in row.SubItems)
            {
                if (TryToggleChildren(child, parentId, getChildren)) return true;
            }
            return false;
        }

        public static bool TrySetRecommendationStatus(GenericItemList list, long itemId, string message, bool success)
        {
            foreach (var row in list)
            {
                if (row.Button2?.CommandId == RecommendCommands.BuildSendCommandId(itemId))
                {
                    row.SecondaryText = message;
                    row.Status = success ? ItemStatus.Succeeded : ItemStatus.Failed;
                    return true;
                }
                if (row.SubItems != null && TrySetRecommendationStatus(row.SubItems, itemId, message, success)) return true;
            }
            return false;
        }

        private static bool TrySetRecommendationStatus(IEnumerable<GenericListItem> rows, long itemId, string message, bool success)
        {
            foreach (var row in rows)
            {
                if (row.Button2?.CommandId == RecommendCommands.BuildSendCommandId(itemId))
                {
                    row.SecondaryText = message;
                    row.Status = success ? ItemStatus.Succeeded : ItemStatus.Failed;
                    return true;
                }
                if (row.SubItems != null && TrySetRecommendationStatus(row.SubItems, itemId, message, success)) return true;
            }
            return false;
        }

        private static bool CanExpand(string type) => type == RecommendableMediaTypes.Series || type == RecommendableMediaTypes.Season || type == RecommendableMediaTypes.MusicArtist || type == RecommendableMediaTypes.MusicAlbum;

        private static string FormatTitle(BaseItem item)
        {
            if (item is Episode episode)
                return $"S{episode.ParentIndexNumber.GetValueOrDefault():00}E{episode.IndexNumber.GetValueOrDefault():00} {episode.Name} - {episode.SeriesName}";
            if (item is MusicAlbum album)
                return JoinNonEmpty(album.Name, string.Join(", ", album.AlbumArtists ?? System.Array.Empty<string>()));
            if (item is Audio song)
                return JoinNonEmpty(song.Name, string.Join(", ", song.Artists ?? System.Array.Empty<string>()), song.Album);
            return item.Name;
        }

        private static string JoinNonEmpty(params string[] values) => string.Join(" - ", values.Where(v => !string.IsNullOrWhiteSpace(v)));

        internal static IconNames GetIcon(string type)
        {
            switch (type)
            {
                case RecommendableMediaTypes.MusicArtist: return IconNames.person_pin;
                case RecommendableMediaTypes.Person: return IconNames.person;
                case RecommendableMediaTypes.Song: return IconNames.music_note;
                case RecommendableMediaTypes.MusicAlbum: return IconNames.music_video;
                case RecommendableMediaTypes.Movie: return IconNames.video_library;
                case RecommendableMediaTypes.BoxSet: return IconNames.folder_special;
                case RecommendableMediaTypes.Series: return IconNames.tv;
                default: return type.IndexOf("Person", System.StringComparison.OrdinalIgnoreCase) >= 0 ? IconNames.person_pin_circle : IconNames.input;
            }
        }

        /// <summary>Resolves the Value posted back from TargetUserChoices (a raw username) to a real User.</summary>
        public static User ResolveTargetUser(string selectedValue, User currentUser)
        {
            if (string.IsNullOrEmpty(selectedValue))
            {
                return null;
            }

            if (selectedValue == currentUser.Name)
            {
                return currentUser;
            }

            return Plugin.Instance.GetAllUsers().FirstOrDefault(u => u.Name == selectedValue);
        }

        public static GenericItemList BuildStatusMessage(string primaryText, bool success)
        {
            return new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = primaryText,
                    Status = success ? ItemStatus.Succeeded : ItemStatus.Failed,
                    Icon = success ? IconNames.check_circle : IconNames.error
                }
            };
        }
    }
}
