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
            var selected = settings.Groups.FirstOrDefault(g => g.Id == state.SelectedGroupId);
            state.GroupResults = new GenericItemList(settings.Groups
                .Where(g => Contains(g.Name, state.GroupSearch)).OrderBy(g => g.Name).Take(10)
                .Select(g => new GenericListItem
                {
                    PrimaryText = g.Name,
                    SecondaryText = MemberNames(g, users),
                    Icon = IconNames.groups,
                    Button1 = new ButtonItem("Manage") { CommandId = GroupsCommands.Select(g.Id) }
                }));

            state.CurrentMembers = selected == null ? "Select a group." : MemberNames(selected, users);
            state.RenameName = selected?.Name ?? state.RenameName;
            state.GroupActions = selected == null ? new GenericItemList() : new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = selected.Name,
                    SecondaryText = selected.MemberUserIds.Count == 0 ? "Empty; deletion is allowed" : "Remove all members before deleting",
                    Icon = IconNames.groups,
                    Button1 = new ButtonItem("Rename") { CommandId = GroupsCommands.Rename },
                    Button2 = new ButtonItem("Delete") { CommandId = GroupsCommands.Delete, IsEnabled = selected.MemberUserIds.Count == 0 }
                }
            };

            state.UserResults = new GenericItemList();
            if (selected != null && !string.IsNullOrWhiteSpace(state.UserSearch))
            {
                foreach (var user in users.Where(u => Contains(u.Name, state.UserSearch)).OrderBy(u => u.Name).Take(10))
                {
                    var member = selected.MemberUserIds.Contains(user.InternalId);
                    state.UserResults.Add(new GenericListItem
                    {
                        PrimaryText = user.Name,
                        SecondaryText = member ? "Current member" : "Not a member",
                        Icon = IconNames.person,
                        Button1 = new ButtonItem(member ? "Remove" : "Add") { CommandId = GroupsCommands.ToggleUser(selected.Id, user.InternalId) }
                    });
                }
            }

            state.MembershipResults = new GenericItemList();
            if (!string.IsNullOrWhiteSpace(state.MembershipUserSearch))
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
