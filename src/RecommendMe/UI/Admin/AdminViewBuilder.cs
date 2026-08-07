using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;

namespace RecommendMe.UI.Admin
{
    internal static class AdminViewBuilder
    {
        internal static GenericItemList BuildMediaTypeList(List<string> allowed)
        {
            return new GenericItemList(RecommendableMediaTypes.All.Select(mediaType =>
            {
                var included = allowed.Contains(mediaType);
                return new GenericListItem
                {
                    PrimaryText = mediaType,
                    Icon = global::RecommendMe.UI.Recommend.RecommendViewBuilder.GetIcon(mediaType),
                    Status = included ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed")
                    {
                        IsChecked = included,
                        CommandId = AdminCommands.BuildMediaTypeToggle(mediaType)
                    }
                };
            }));
        }

        public static AdminSettingsUI Build(AdminSettings settings, IReadOnlyList<User> users, AdminSettingsUI state)
        {
            state = state ?? new AdminSettingsUI();
            state.AlwaysExpandUsersAndGroups = settings.AlwaysExpandUsersAndGroups;
            if (settings.AlwaysExpandUsersAndGroups)
            {
                state.UserSearch = string.Empty;
            }

            state.NewUserDefaultsList = BuildDefaultPolicy(settings, users);

            var matches = users.Where(u => Contains(u.Name, state.UserSearch)).OrderBy(u => u.Name).ToArray();
            var visibleUsers = settings.AlwaysExpandUsersAndGroups ? matches : matches.Take(10).ToArray();
            state.UserSearchSummary = new LabelItem(SearchSummary(state.UserSearch, visibleUsers.Length, matches.Length, users.Count, "users"));
            state.UserAccessList = new GenericItemList();
            foreach (var user in visibleUsers)
            {
                var entry = settings.UserAccess.First(e => e.UserId == user.InternalId);
                var groups = GroupNames(settings, entry.UserId);
                state.UserAccessList.Add(new GenericListItem
                {
                    PrimaryText = $"{user.Name} - {PolicyName(entry.SendPolicy)} / New User Allowed {(entry.AllowNewUsers ? "Y" : "N")}",
                    SecondaryText = "Groups: " + (groups.Length == 0 ? "None" : string.Join(", ", groups)),
                    Icon = IconNames.person,
                    Status = entry.AccessSuspended ? ItemStatus.Failed : ItemStatus.Succeeded,
                    Toggle = new ToggleButtonItem("Suspended")
                    {
                        IsChecked = entry.AccessSuspended,
                        CommandId = AdminCommands.Suspended(entry.UserId)
                    },
                    Button1 = new ButtonItem
                    {
                        Caption = "Manage",
                        SubMenuButtons = new List<ButtonItem>
                        {
                            new ButtonItem("Send to") { CommandId = AdminCommands.SendTo(entry.UserId) },
                            new ButtonItem("Receive from") { CommandId = AdminCommands.ReceiveFrom(entry.UserId) },
                            new ButtonItem("Group Membership") { CommandId = AdminCommands.Membership(entry.UserId) }
                        }
                    }
                });
            }

            return state;
        }

        internal static string PolicyName(SendPolicyType policy)
        {
            switch (policy)
            {
                case SendPolicyType.Everyone: return "Everyone";
                case SendPolicyType.NoOne: return "No One";
                case SendPolicyType.AllowedUsers: return "Allowed Users";
                case SendPolicyType.GroupMembers: return "Group Members";
                default: return policy.ToString();
            }
        }

        internal static string[] GroupNames(AdminSettings settings, long userId) =>
            settings.Groups.Where(g => g.MemberUserIds.Contains(userId)).Select(g => g.Name).OrderBy(n => n).ToArray();

        internal static bool Contains(string value, string search) =>
            string.IsNullOrWhiteSpace(search) || (value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

        internal static string SearchSummary(string search, int shown, int matches, int total, string noun)
        {
            return string.IsNullOrWhiteSpace(search)
                ? $"Showing {shown} of {total} {noun} (no filter)."
                : $"Showing {shown} of {matches} matching {noun} for ‘{search.Trim()}’ ({total} total).";
        }

        private static GenericItemList BuildDefaultPolicy(AdminSettings settings, IReadOnlyList<User> users)
        {
            var source = settings.DefaultUserPolicySourceUserId.HasValue
                ? settings.UserAccess.FirstOrDefault(e => e.UserId == settings.DefaultUserPolicySourceUserId.Value)
                : null;
            var sourceUser = source == null ? null : users.FirstOrDefault(u => u.InternalId == source.UserId);
            var text = source == null
                ? "No default user selected - new users use No One and no groups"
                : $"{sourceUser?.Name ?? source.UserName} - {PolicyName(source.SendPolicy)} / New User Allowed {(source.AllowNewUsers ? "Y" : "N")}";
            var groups = source == null ? new string[0] : GroupNames(settings, source.UserId);

            return new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = text,
                    SecondaryText = "Groups: " + (groups.Length == 0 ? "None" : string.Join(", ", groups)),
                    Icon = IconNames.person,
                    Button1 = new ButtonItem("Default User Policy") { CommandId = AdminCommands.DefaultPolicyRefresh }
                }
            };
        }
    }
}
