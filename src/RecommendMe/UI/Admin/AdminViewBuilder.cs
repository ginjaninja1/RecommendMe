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
            var ui = new AdminSettingsUI
            {
                SendScopeList = BuildScopeList(
                    "Who can send recommendations",
                    settings.SendScope,
                    settings.SendScopeUserIds,
                    allUsers,
                    AdminCommands.BuildSendScopeModeToggle(),
                    AdminCommands.BuildSendScopeUserToggle),

                ReceiveScopeList = BuildScopeList(
                    "Who can receive recommendations",
                    settings.ReceiveScope,
                    settings.ReceiveScopeUserIds,
                    allUsers,
                    AdminCommands.BuildReceiveScopeModeToggle(),
                    AdminCommands.BuildReceiveScopeUserToggle),

                MediaTypeList = BuildMediaTypeList(settings.GloballyAllowedMediaTypes, AdminCommands.BuildMediaTypeToggle),

                DefaultProfileList = BuildDefaultProfileList(settings.DefaultProfile),

                UserAccessList = BuildUserAccessList(settings)
            };

            return ui;
        }

        private static GenericItemList BuildScopeList(
            string title,
            AccessScope scope,
            List<long> scopeUserIds,
            IReadOnlyList<User> allUsers,
            string modeCommandId,
            System.Func<long, string> buildUserCommandId)
        {
            var list = new GenericItemList();

            var modeItem = new GenericListItem
            {
                PrimaryText = title,
                SecondaryText = scope == AccessScope.AllUsers ? "All Users" : "Specific Named Users (see below)",
                Status = ItemStatus.Succeeded,
                Toggle = new ToggleButtonItem("Restrict to specific users")
                {
                    IsChecked = scope == AccessScope.SpecificUsers,
                    CommandId = modeCommandId
                }
            };

            if (scope == AccessScope.SpecificUsers)
            {
                var subItems = new GenericItemList();
                foreach (var user in allUsers.OrderBy(u => u.Name))
                {
                    subItems.Add(new GenericListItem
                    {
                        PrimaryText = user.Name,
                        Status = scopeUserIds.Contains(user.InternalId) ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Included")
                        {
                            IsChecked = scopeUserIds.Contains(user.InternalId),
                            CommandId = buildUserCommandId(user.InternalId)
                        }
                    });
                }

                modeItem.SubItems = subItems;
            }

            list.Add(modeItem);
            return list;
        }

        private static GenericItemList BuildMediaTypeList(List<string> allowed, System.Func<string, string> buildCommandId)
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
                        CommandId = buildCommandId(mediaType)
                    }
                });
            }

            return list;
        }

        private static GenericItemList BuildDefaultProfileList(DefaultUserProfile defaultProfile)
        {
            var list = new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = "Allow sending (new users)",
                    Status = defaultProfile.AllowSending ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed") { IsChecked = defaultProfile.AllowSending, CommandId = "defaultsending" }
                },
                new GenericListItem
                {
                    PrimaryText = "Allow receiving (new users)",
                    Status = defaultProfile.AllowReceiving ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed") { IsChecked = defaultProfile.AllowReceiving, CommandId = "defaultreceiving" }
                }
            };

            foreach (var mediaType in RecommendableMediaTypes.All)
            {
                list.Add(new GenericListItem
                {
                    PrimaryText = $"  {mediaType}",
                    Status = defaultProfile.AllowedMediaTypes.Contains(mediaType) ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed")
                    {
                        IsChecked = defaultProfile.AllowedMediaTypes.Contains(mediaType),
                        CommandId = AdminCommands.BuildDefaultMediaTypeToggle(mediaType)
                    }
                });
            }

            return list;
        }

        private static GenericItemList BuildUserAccessList(AdminSettings settings)
        {
            var list = new GenericItemList();

            foreach (var entry in settings.UserAccess.OrderBy(u => u.UserName))
            {
                var subItems = new GenericItemList
                {
                    new GenericListItem
                    {
                        PrimaryText = "Can Send",
                        Status = entry.AllowSending ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Allowed")
                        {
                            IsChecked = entry.AllowSending,
                            CommandId = AdminCommands.BuildUserSendingToggle(entry.UserId)
                        }
                    },
                    new GenericListItem
                    {
                        PrimaryText = "Can Receive",
                        Status = entry.AllowReceiving ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Allowed")
                        {
                            IsChecked = entry.AllowReceiving,
                            CommandId = AdminCommands.BuildUserReceivingToggle(entry.UserId)
                        }
                    }
                };

                foreach (var mediaType in RecommendableMediaTypes.All)
                {
                    subItems.Add(new GenericListItem
                    {
                        PrimaryText = $"  {mediaType}",
                        Status = entry.AllowedMediaTypes.Contains(mediaType) ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Allowed")
                        {
                            IsChecked = entry.AllowedMediaTypes.Contains(mediaType),
                            CommandId = AdminCommands.BuildUserMediaTypeToggle(entry.UserId, mediaType)
                        }
                    });
                }

                var revoked = !entry.AllowSending && !entry.AllowReceiving;

                list.Add(new GenericListItem
                {
                    PrimaryText = entry.UserName,
                    SecondaryText = revoked ? "Access revoked" : null,
                    Status = revoked ? ItemStatus.Failed : ItemStatus.Succeeded,
                    SubItems = subItems
                });
            }

            return list;
        }
    }
}
