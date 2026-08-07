using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.Models;
using RecommendMe.Services;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    public class SendToUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;

        [Browsable(false)]
        public List<EditorSelectOption> SendPolicyTypes { get; set; } = new List<EditorSelectOption>
        {
            new EditorSelectOption(nameof(SendPolicyType.Everyone), "Everyone"),
            new EditorSelectOption(nameof(SendPolicyType.NoOne), "No One"),
            new EditorSelectOption(nameof(SendPolicyType.AllowedUsers), "Allowed Users"),
            new EditorSelectOption(nameof(SendPolicyType.GroupMembers), "Group Members")
        };

        [DisplayName("Select Send Policy")]
        [SelectItemsSource(nameof(SendPolicyTypes))]
        [AutoPostBack(AdminCommands.SendToRefresh, nameof(SelectedSendPolicy))]
        public string SelectedSendPolicy { get; set; }

        public GenericItemList NewUserSetting { get; set; } = new GenericItemList();

        [DisplayName("Username filter")]
        [Description("Leave blank to show users. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.SendToRefresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;

        public GenericItemList ExistingUsers { get; set; } = new GenericItemList();
    }

    internal class SendToDialogView : AdminDialogViewBase
    {
        private readonly long userId;

        public SendToDialogView(string pluginId, long userId, IPluginUIView parent, Action rebuildParent, IServerApplicationHost host, ILogger logger)
            : base(pluginId, parent, rebuildParent, host, logger)
        {
            this.userId = userId;
            this.Rebuild(new SendToUI());
        }

        public override string Caption => "Send To: " + this.UserName(this.userId);

        private void Rebuild(SendToUI state)
        {
            var settings = this.Settings();
            var owner = settings.UserAccess.First(e => e.UserId == this.userId);
            state.SelectedSendPolicy = owner.SendPolicy.ToString();
            state.NewUserSetting = new GenericItemList
            {
                new GenericListItem
                {
                    PrimaryText = "Add new users as allowed",
                    SecondaryText = owner.AllowNewUsers
                        ? "New users will be added to this user's allowed-user specification"
                        : "New users will not be added automatically",
                    Status = owner.AllowNewUsers ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                    Toggle = new ToggleButtonItem("Allowed")
                    {
                        IsChecked = owner.AllowNewUsers,
                        CommandId = AdminCommands.ToggleAllowNewUsers
                    }
                }
            };

            state.ExistingUsers = new GenericItemList();
            if (settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.UserSearch))
            {
                var active = owner.SendPolicy == SendPolicyType.AllowedUsers;
                foreach (var user in Plugin.Instance.GetAllUsers().Where(u => u.InternalId != this.userId && AdminViewBuilder.Contains(u.Name, state.UserSearch)).OrderBy(u => u.Name).Take(10))
                {
                    var allowed = owner.AllowedTargetUserIds.Contains(user.InternalId);
                    state.ExistingUsers.Add(new GenericListItem
                    {
                        PrimaryText = user.Name,
                        Icon = IconNames.person,
                        Status = allowed ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Toggle = new ToggleButtonItem("Allowed")
                        {
                            IsChecked = allowed,
                            IsEnabled = active,
                            CommandId = AdminCommands.Target(this.userId, user.InternalId)
                        }
                    });
                }
            }
            this.ContentData = state;
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (this.IsCancel(commandId)) return this.ReturnToParent();
            var state = this.ReadState<SendToUI>(data);

            if (commandId == AdminCommands.SendToRefresh && Enum.TryParse(state.SelectedSendPolicy, out SendPolicyType policy))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var owner = s.UserAccess.FirstOrDefault(e => e.UserId == this.userId);
                    if (owner != null) owner.SendPolicy = policy;
                }).GetAwaiter().GetResult();
            }
            else if (commandId == AdminCommands.ToggleAllowNewUsers)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var owner = s.UserAccess.FirstOrDefault(e => e.UserId == this.userId);
                    if (owner != null) owner.AllowNewUsers = !owner.AllowNewUsers;
                }).GetAwaiter().GetResult();
            }
            else if (AdminCommands.TryTarget(commandId, out var ownerId, out var targetId) && ownerId == this.userId)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var owner = s.UserAccess.FirstOrDefault(e => e.UserId == this.userId);
                    if (owner == null || owner.SendPolicy != SendPolicyType.AllowedUsers) return;
                    Toggle(owner.AllowedTargetUserIds, targetId);
                }).GetAwaiter().GetResult();
            }

            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }
    }

    public class ReceiveFromUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        [DisplayName("Username filter")]
        [Description("Leave blank to show users. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.ReceiveFromRefresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;
        public GenericItemList SenderList { get; set; } = new GenericItemList();
    }

    internal class ReceiveFromDialogView : AdminDialogViewBase
    {
        private readonly long userId;
        public ReceiveFromDialogView(string pluginId, long userId, IPluginUIView parent, Action rebuildParent, IServerApplicationHost host, ILogger logger)
            : base(pluginId, parent, rebuildParent, host, logger)
        {
            this.userId = userId;
            this.Rebuild(new ReceiveFromUI());
        }
        public override string Caption => "Receive From: " + this.UserName(this.userId);

        private void Rebuild(ReceiveFromUI state)
        {
            var settings = this.Settings();
            var preferences = Plugin.Instance.UserPreferenceStore.GetForUserAsync(this.userId).GetAwaiter().GetResult();
            state.SenderList = new GenericItemList();
            if (settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.UserSearch))
            {
                foreach (var sender in Plugin.Instance.GetAllUsers().Where(u => u.InternalId != this.userId && AdminViewBuilder.Contains(u.Name, state.UserSearch)).OrderBy(u => u.Name).Take(10))
                {
                    var senderEntry = settings.UserAccess.First(e => e.UserId == sender.InternalId);
                    var preference = preferences.SenderPreferences.FirstOrDefault(p => p.SenderUserId == sender.InternalId);
                    var blocked = preference?.Blocked ?? false;
                    var optedOut = preference?.OptedOutMediaTypes ?? new List<string>();
                    var mediaItems = new GenericItemList();
                    foreach (var mediaType in RecommendableMediaTypes.All.Where(settings.GloballyAllowedMediaTypes.Contains))
                    {
                        var receive = !optedOut.Contains(mediaType);
                        mediaItems.Add(new GenericListItem
                        {
                            PrimaryText = mediaType,
                            Icon = global::RecommendMe.UI.Recommend.RecommendViewBuilder.GetIcon(mediaType),
                            Status = receive ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                            Toggle = new ToggleButtonItem("Receive")
                            {
                                IsChecked = receive && !blocked,
                                IsEnabled = !blocked,
                                CommandId = AdminCommands.ReceiveMedia(this.userId, sender.InternalId, mediaType)
                            }
                        });
                    }
                    var permitted = !senderEntry.AccessSuspended && PermissionService.IsTargetAllowed(senderEntry, this.userId, settings);
                    state.SenderList.Add(new GenericListItem
                    {
                        PrimaryText = sender.Name,
                        SecondaryText = permitted ? "Permitted by send policy" : "Not currently permitted by send policy",
                        Icon = IconNames.person,
                        Status = blocked ? ItemStatus.Failed : ItemStatus.Succeeded,
                        Toggle = new ToggleButtonItem("Accept recommendations")
                        {
                            IsChecked = !blocked,
                            CommandId = AdminCommands.ReceiveSender(this.userId, sender.InternalId)
                        },
                        SubItems = mediaItems
                    });
                }
            }
            this.ContentData = state;
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (this.IsCancel(commandId)) return this.ReturnToParent();
            var state = this.ReadState<ReceiveFromUI>(data);
            var prefs = Plugin.Instance.UserPreferenceStore.GetForUserAsync(this.userId).GetAwaiter().GetResult();
            var changed = false;
            if (AdminCommands.TryReceiveSender(commandId, out var ownerId, out var senderId) && ownerId == this.userId)
            {
                var pref = GetPreference(prefs, senderId);
                pref.Blocked = !pref.Blocked;
                changed = true;
            }
            else if (AdminCommands.TryReceiveMedia(commandId, out ownerId, out senderId, out var mediaType) && ownerId == this.userId)
            {
                var pref = GetPreference(prefs, senderId);
                if (!pref.Blocked) Toggle(pref.OptedOutMediaTypes, mediaType);
                changed = !pref.Blocked;
            }
            if (changed)
            {
                prefs.UserId = this.userId;
                Plugin.Instance.UserPreferenceStore.SaveForUserAsync(prefs).GetAwaiter().GetResult();
            }
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }

        private static SenderPreference GetPreference(UserReceivePreferences prefs, long senderId)
        {
            var value = prefs.SenderPreferences.FirstOrDefault(p => p.SenderUserId == senderId);
            if (value == null)
            {
                value = new SenderPreference { SenderUserId = senderId };
                prefs.SenderPreferences.Add(value);
            }
            return value;
        }
    }

    public class UserGroupMembershipUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        public CaptionItem CurrentHeading { get; set; } = new CaptionItem("Current Groups");
        public LabelItem CurrentGroups { get; set; } = new LabelItem(string.Empty);
        [DisplayName("Group name filter")]
        [Description("Leave blank to show groups. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.GroupMembershipRefresh, nameof(GroupSearch))]
        public string GroupSearch { get; set; } = string.Empty;
        public GenericItemList GroupResults { get; set; } = new GenericItemList();
    }

    internal class UserGroupMembershipDialogView : AdminDialogViewBase
    {
        private readonly long userId;
        public UserGroupMembershipDialogView(string pluginId, long userId, IPluginUIView parent, Action rebuildParent, IServerApplicationHost host, ILogger logger)
            : base(pluginId, parent, rebuildParent, host, logger)
        {
            this.userId = userId;
            this.Rebuild(new UserGroupMembershipUI());
        }
        public override string Caption => "Group Membership: " + this.UserName(this.userId);
        private void Rebuild(UserGroupMembershipUI state)
        {
            var settings = this.Settings();
            var current = AdminViewBuilder.GroupNames(settings, this.userId);
            state.CurrentGroups = new LabelItem(current.Length == 0 ? "None" : string.Join(", ", current));
            state.GroupResults = new GenericItemList();
            if (settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.GroupSearch))
            {
                foreach (var group in settings.Groups.Where(g => AdminViewBuilder.Contains(g.Name, state.GroupSearch)).OrderBy(g => g.Name).Take(10))
                {
                    var member = group.MemberUserIds.Contains(this.userId);
                    state.GroupResults.Add(new GenericListItem
                    {
                        PrimaryText = group.Name,
                        Icon = IconNames.groups,
                        Status = member ? ItemStatus.Succeeded : ItemStatus.Unavailable,
                        Button1 = new ButtonItem(member ? "Remove" : "Add") { CommandId = AdminCommands.Group(this.userId, group.Id) }
                    });
                }
            }
            this.ContentData = state;
        }
        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (this.IsCancel(commandId)) return this.ReturnToParent();
            var state = this.ReadState<UserGroupMembershipUI>(data);
            if (AdminCommands.TryGroup(commandId, out var userId, out var groupId) && userId == this.userId)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null) Toggle(group.MemberUserIds, this.userId);
                }).GetAwaiter().GetResult();
            }
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }
    }

    public class DefaultUserPolicyUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        public CaptionItem CurrentHeading { get; set; } = new CaptionItem("Current default user to copy");
        public GenericItemList CurrentDefault { get; set; } = new GenericItemList();
        [DisplayName("Username filter")]
        [Description("Leave blank to show users. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.DefaultPolicyRefresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;
        public GenericItemList UserResults { get; set; } = new GenericItemList();
    }

    internal class DefaultUserPolicyDialogView : AdminDialogViewBase
    {
        public DefaultUserPolicyDialogView(string pluginId, IPluginUIView parent, Action rebuildParent, IServerApplicationHost host, ILogger logger)
            : base(pluginId, parent, rebuildParent, host, logger) => this.Rebuild(new DefaultUserPolicyUI());
        public override string Caption => "Default User Policy";
        private void Rebuild(DefaultUserPolicyUI state)
        {
            var settings = this.Settings();
            state.CurrentDefault = BuildUsers(settings, Plugin.Instance.GetAllUsers().Where(u => settings.DefaultUserPolicySourceUserId == u.InternalId), false);
            if (state.CurrentDefault.Count == 0)
            {
                state.CurrentDefault.Add(new GenericListItem { PrimaryText = "No default user selected", SecondaryText = "New users use No One and no groups", Status = ItemStatus.Unavailable });
            }
            state.UserResults = new GenericItemList();
            if (settings.AlwaysExpandUsersAndGroups || !string.IsNullOrWhiteSpace(state.UserSearch))
            {
                state.UserResults = BuildUsers(settings, Plugin.Instance.GetAllUsers().Where(u => AdminViewBuilder.Contains(u.Name, state.UserSearch)).OrderBy(u => u.Name).Take(10), true);
            }
            this.ContentData = state;
        }
        private static GenericItemList BuildUsers(AdminSettings settings, IEnumerable<User> users, bool selectable)
        {
            return new GenericItemList(users.Select(user =>
            {
                var entry = settings.UserAccess.First(e => e.UserId == user.InternalId);
                var groups = AdminViewBuilder.GroupNames(settings, user.InternalId);
                return new GenericListItem
                {
                    PrimaryText = $"{user.Name} - {AdminViewBuilder.PolicyName(entry.SendPolicy)} / New User Allowed {(entry.AllowNewUsers ? "Y" : "N")}",
                    SecondaryText = "Groups: " + (groups.Length == 0 ? "None" : string.Join(", ", groups)),
                    Icon = IconNames.person,
                    Button1 = selectable ? new ButtonItem("Select") { CommandId = AdminCommands.SelectDefault(user.InternalId) } : null
                };
            }));
        }
        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (this.IsCancel(commandId)) return this.ReturnToParent();
            var state = this.ReadState<DefaultUserPolicyUI>(data);
            if (AdminCommands.TrySelectDefault(commandId, out var userId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    if (s.UserAccess.Any(e => e.UserId == userId)) s.DefaultUserPolicySourceUserId = userId;
                }).GetAwaiter().GetResult();
            }
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }
    }

    internal abstract class AdminDialogViewBase : PluginDialogView
    {
        private readonly IPluginUIView parent;
        private readonly Action rebuildParent;
        protected readonly IJsonSerializer Serializer;
        protected readonly ILogger Logger;

        protected AdminDialogViewBase(string pluginId, IPluginUIView parent, Action rebuildParent, IServerApplicationHost host, ILogger logger)
            : base(pluginId)
        {
            this.parent = parent;
            this.rebuildParent = rebuildParent;
            this.Serializer = host.Resolve<IJsonSerializer>();
            this.Logger = logger;
            this.AllowOk = false;
            this.AllowCancel = true;
        }

        protected AdminSettings Settings() => Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
        protected string UserName(long id) => Plugin.Instance.GetAllUsers().FirstOrDefault(u => u.InternalId == id)?.Name ?? id.ToString();
        protected T ReadState<T>(string data) where T : class => string.IsNullOrEmpty(data) ? (T)this.ContentData : this.Serializer.DeserializeFromString<T>(data) ?? (T)this.ContentData;
        protected bool IsCancel(string command) => string.Equals(command, "DialogCancel", StringComparison.OrdinalIgnoreCase);
        protected Task<IPluginUIView> ReturnToParent() { this.rebuildParent(); return Task.FromResult(this.parent); }
        protected static void Toggle<T>(List<T> values, T value) { if (values.Contains(value)) values.Remove(value); else values.Add(value); }
        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;
        public override Task Cancel() => Task.CompletedTask;
    }
}
