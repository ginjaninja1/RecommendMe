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
        public static AdminSettingsUI Build(AdminSettings settings, IReadOnlyList<User> allUsers)
        {
            return new AdminSettingsUI
            {
                MediaTypeList = BuildMediaTypeList(settings.GloballyAllowedMediaTypes),

                NewUserDefaultsList = BuildNewUserDefaultsList(settings),

                UserAccessList = BuildUserAccessList(settings, allUsers)
            };
        }

        private static GenericItemList BuildMediaTypeList(List<string> allowed)
        {
            var list = new GenericItemList();

            foreach (var mediaType in RecommendableMediaTypes.All)
            {
                list.Add(new GenericListItem
                {
                    PrimaryText = mediaType,
                    Status = allowed.Contains(mediaType) ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed")
                    {
                        IsChecked = allowed.Contains(mediaType),
                        CommandId = AdminCommands.BuildMediaTypeToggle(mediaType)
                    }
                });
            }

            return list;
        }

        private static GenericItemList BuildNewUserDefaultsList(AdminSettings settings)
        {
            var newUsersCanSendToEveryone = settings.NewUserDefaultSendMode == SendMode.Everyone;

            return new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = "New users can send to everyone by default",
                    SecondaryText = newUsersCanSendToEveryone
                        ? "Everyone"
                        : "No One (admin must grant recipients manually)",
                    Status = newUsersCanSendToEveryone ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Everyone")
                    {
                        IsChecked = newUsersCanSendToEveryone,
                        CommandId = AdminCommands.BuildNewUserDefaultSendModeToggle()
                    }
                },
                new GenericListItem
                {
                    PrimaryText = "Auto-add new users to existing users' allow-lists",
                    SecondaryText = settings.AutoGrantNewUsersToExistingSendLists
                        ? "New users are added as an allowed recipient for everyone using a named list"
                        : "Existing named lists are left untouched - admin adds new users manually",
                    Status = settings.AutoGrantNewUsersToExistingSendLists ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed")
                    {
                        IsChecked = settings.AutoGrantNewUsersToExistingSendLists,
                        CommandId = AdminCommands.BuildAutoGrantToggle()
                    }
                }
            };
        }

        private static GenericItemList BuildUserAccessList(AdminSettings settings, IReadOnlyList<User> allUsers)
        {
            var list = new GenericItemList();

            foreach (var entry in settings.UserAccess.OrderBy(u => u.UserName))
            {
                var subItems = new GenericItemList
                {
                    new GenericListItem
                    {
                        PrimaryText = "Access suspended (Emergency Revocation)",
                        SecondaryText = entry.AccessSuspended
                            ? "Blocked from sending and receiving"
                            : "Not suspended",
                        Status = entry.AccessSuspended ? ItemStatus.Failed : ItemStatus.Succeeded,
                        Toggle = new ToggleButtonItem("Suspended")
                        {
                            IsChecked = entry.AccessSuspended,
                            CommandId = AdminCommands.BuildUserSuspendedToggle(entry.UserId)
                        }
                    }
                };

                if (entry.SendMode == SendMode.SpecificUsers)
                {
                    foreach (var target in allUsers.Where(u => u.InternalId != entry.UserId).OrderBy(u => u.Name))
                    {
                        var included = entry.AllowedTargetUserIds.Contains(target.InternalId);
                        subItems.Add(new GenericListItem
                        {
                            PrimaryText = $"  Can send to: {target.Name}",
                            Status = included ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                            Toggle = new ToggleButtonItem("Included")
                            {
                                IsChecked = included,
                                CommandId = AdminCommands.BuildUserTargetToggle(entry.UserId, target.InternalId)
                            }
                        });
                    }
                }

                list.Add(new GenericListItem
                {
                    PrimaryText = entry.UserName,
                    SecondaryText = DescribeSendMode(entry),
                    Status = entry.AccessSuspended ? ItemStatus.Failed : ItemStatus.Succeeded,
                    Button1 = BuildSendModeButton(entry),
                    SubItems = subItems
                });
            }

            return list;
        }

        private static string DescribeSendMode(UserAccessEntry entry)
        {
            switch (entry.SendMode)
            {
                case SendMode.Everyone:
                    return "Can send to: Everyone";
                case SendMode.NoOne:
                    return "Can send to: No One";
                case SendMode.SpecificUsers:
                    return $"Can send to: {entry.AllowedTargetUserIds.Count} named user(s) - see below";
                default:
                    return null;
            }
        }

        private static ButtonItem BuildSendModeButton(UserAccessEntry entry)
        {
            return new ButtonItem
            {
                Caption = "Change Send Mode",
                SubMenuButtons = new List<ButtonItem>
                {
                    new ButtonItem("Everyone") { CommandId = AdminCommands.BuildUserSendModeCommand(entry.UserId, SendMode.Everyone) },
                    new ButtonItem("No One") { CommandId = AdminCommands.BuildUserSendModeCommand(entry.UserId, SendMode.NoOne) },
                    new ButtonItem("Specific Users") { CommandId = AdminCommands.BuildUserSendModeCommand(entry.UserId, SendMode.SpecificUsers) }
                }
            };
        }
    }
}