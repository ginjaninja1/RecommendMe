using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;

namespace RecommendMe.UI.Admin
{
    internal static class GroupsViewBuilder
    {
        public static GroupsUI Build(AdminSettings settings, IReadOnlyList<User> users, GroupsUI state)
        {
            state = state ?? new GroupsUI();
            var matches = settings.Groups.Where(g => Contains(g.Name, state.GroupSearch)).OrderBy(g => g.Name).ToArray();
            state.GroupSearchSummary = new LabelItem($"{matches.Length} matches, {settings.Groups.Count} Groups total");
            var visibleGroups = settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.GroupSearch)
                ? matches.Take(10)
                : Enumerable.Empty<UserGroup>();
            state.GroupResults = new GenericItemList(visibleGroups
                .Select(g => new GenericListItem
                {
                    PrimaryText = g.Name,
                    SecondaryText = MemberNames(g, users),
                    Icon = IconNames.groups,
                    Button1 = new ButtonItem
                    {
                        Caption = "Manage",
                        SubMenuButtons = new List<ButtonItem>
                        {
                            new ButtonItem("Add/remove users") { CommandId = GroupsCommands.Members(g.Id) },
                            new ButtonItem("Rename") { CommandId = GroupsCommands.Rename(g.Id) },
                            new ButtonItem("Delete") { CommandId = GroupsCommands.Delete(g.Id) }
                        }
                    }
                }));

            state.MembershipResults = new GenericItemList();
            if (settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.MembershipUserSearch))
            {
                foreach (var user in users.Where(u => Contains(u.Name, state.MembershipUserSearch)).OrderBy(u => u.Name).Take(10))
                {
                    var memberships = settings.Groups.Where(g => g.MemberUserIds.Contains(user.InternalId)).Select(g => g.Name);
                    state.MembershipResults.Add(new GenericListItem { PrimaryText = user.Name, SecondaryText = string.Join(", ", memberships), Icon = IconNames.person });
                }
            }
            return state;
        }

        private static bool Contains(string value, string search) => string.IsNullOrWhiteSpace(search) || (value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        private static string MemberNames(UserGroup group, IReadOnlyList<User> users)
        {
            var names = users.Where(u => group.MemberUserIds.Contains(u.InternalId)).Select(u => u.Name).OrderBy(n => n).ToArray();
            return names.Length == 0 ? "No members" : string.Join(", ", names);
        }
    }
}
