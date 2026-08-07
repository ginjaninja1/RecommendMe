using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
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

            var currentUserEntry = await plugin.PermissionService.EnsureUserAccessEntryAsync(currentUser).ConfigureAwait(false);
            if (currentUserEntry.AccessSuspended)
            {
                // Suspended users can still recommend to themselves, but not to anyone else.
                return choices;
            }

            foreach (var candidate in plugin.GetAllUsers())
            {
                if (candidate.InternalId == currentUser.InternalId)
                {
                    continue;
                }

                var candidateEntry = await plugin.PermissionService.EnsureUserAccessEntryAsync(candidate).ConfigureAwait(false);
                if (candidateEntry.AccessSuspended)
                {
                    continue;
                }

                if (PermissionService.IsTargetAllowed(currentUserEntry, candidate.InternalId))
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
                list.Add(new GenericListItem
                {
                    PrimaryText = item.Name,
                    SecondaryText = item.GetType().Name,
                    Icon = IconNames.video_library,
                    IconMode = ItemListIconMode.SmallRegular,
                    Status = ItemStatus.Succeeded,
                    Button1 = new ButtonItem("Recommend")
                    {
                        CommandId = RecommendCommands.BuildSendCommandId(item.InternalId)
                    }
                });
            }

            return list;
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