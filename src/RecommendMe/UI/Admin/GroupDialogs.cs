using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Logging;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    public class GroupMembersUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        [DisplayName("Username search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(UserSearch))]
        public virtual string UserSearch { get; set; } = string.Empty;
        public LabelItem UserSearchSummary { get; set; } = new LabelItem(string.Empty);
        public GenericItemList UserResults { get; set; } = new GenericItemList();
    }

    public class ExpandedGroupMembersUI : GroupMembersUI
    {
        [Browsable(false)]
        public override string UserSearch { get; set; } = string.Empty;
    }

    internal class GroupMembersDialogView : PluginDialogView
    {
        private readonly string groupId;
        private readonly string groupName;
        private readonly IJsonSerializer serializer;
        private readonly IPluginUIView parentPageView;
        private readonly Action rebuildParentContent;
        private readonly ILogger logger;
        public GroupMembersDialogView(
            string pluginId,
            string groupId,
            IPluginUIView parentPageView,
            Action rebuildParentContent,
            IServerApplicationHost host,
            ILogger logger) : base(pluginId)
        {
            this.groupId = groupId;
            this.parentPageView = parentPageView;
            this.rebuildParentContent = rebuildParentContent;
            this.logger = logger;
            this.serializer = host.Resolve<IJsonSerializer>();
            this.groupName = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.First(g => g.Id == groupId).Name;
            this.AllowOk = true;
            this.AllowCancel = true;
            this.Rebuild(new GroupMembersUI());
        }

        public override string Caption => "Add/remove users: " + this.groupName;

        private void Rebuild(GroupMembersUI state)
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            var group = settings.Groups.FirstOrDefault(g => g.Id == this.groupId);
            if (settings.AlwaysExpandUsersAndGroups && !(state is ExpandedGroupMembersUI)) state = new ExpandedGroupMembersUI();
            else if (!settings.AlwaysExpandUsersAndGroups && state is ExpandedGroupMembersUI) state = new GroupMembersUI();
            state.UserResults = new GenericItemList();
            var allUsers = Plugin.Instance.GetAllUsers();
            var matches = allUsers.Where(u => u.Name.IndexOf(state.UserSearch ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(u => u.Name).ToArray();
            var visible = settings.AlwaysExpandUsersAndGroups ? matches : matches.Take(10).ToArray();
            state.UserSearchSummary = new LabelItem(AdminViewBuilder.SearchSummary(state.UserSearch, visible.Length, matches.Length, allUsers.Count, "users"))
            {
                IsVisible = !settings.AlwaysExpandUsersAndGroups
            };
            if (group != null)
            {
                foreach (var user in visible)
                {
                    var member = group.MemberUserIds.Contains(user.InternalId);
                    state.UserResults.Add(new GenericListItem
                    {
                        PrimaryText = user.Name,
                        SecondaryText = member ? "Current member" : "Not a member",
                        Icon = IconNames.person,
                        Button1 = new ButtonItem(member ? "Remove" : "Add") { CommandId = GroupsCommands.ToggleUser(group.Id, user.InternalId) }
                    });
                }
            }
            this.ContentData = state;
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            this.logger.Info("GroupMembersDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.groupName, this.groupId);
            var state = string.IsNullOrEmpty(data) ? (GroupMembersUI)this.ContentData : this.serializer.DeserializeFromString<GroupMembersUI>(data) ?? new GroupMembersUI();
            if (GroupsCommands.TryToggleUser(commandId, out var parsedGroupId, out var userId) && parsedGroupId == this.groupId)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == this.groupId);
                    if (group == null) return;
                    if (group.MemberUserIds.Contains(userId)) group.MemberUserIds.Remove(userId); else group.MemberUserIds.Add(userId);
                }).GetAwaiter().GetResult();
                this.logger.Info("Toggled user {0} membership in group '{1}' ({2})", userId, this.groupName, this.groupId);
            }
            else if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandId, "DialogOk", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("Group membership dialog closed for '{0}' ({1})", this.groupName, this.groupId);
                this.rebuildParentContent();
                return Task.FromResult(this.parentPageView);
            }
            this.Rebuild(state);
            return Task.FromResult<IPluginUIView>(this);
        }

        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;
        public override Task Cancel() => Task.CompletedTask;
    }

    public class RenameGroupUI : EditableOptionsBase
    {
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        [DisplayName("Rename to")]
        public string Name { get; set; }
        public ButtonItem RenameButton { get; set; } = new ButtonItem("Rename")
        {
            StandardIcon = StandardIcons.Edit,
            CommandId = GroupsCommands.ConfirmRename
        };
        public CaptionItem ValidationStatus { get; set; } = new CaptionItem(string.Empty);
    }

    internal class RenameGroupDialogView : PluginDialogView
    {
        private readonly string groupId;
        private readonly string currentName;
        private readonly IJsonSerializer serializer;
        private readonly IPluginUIView parentPageView;
        private readonly Action rebuildParentContent;
        private readonly ILogger logger;
        public RenameGroupDialogView(
            string pluginId,
            string groupId,
            IPluginUIView parentPageView,
            Action rebuildParentContent,
            IServerApplicationHost host,
            ILogger logger) : base(pluginId)
        {
            this.groupId = groupId;
            this.parentPageView = parentPageView;
            this.rebuildParentContent = rebuildParentContent;
            this.logger = logger;
            this.serializer = host.Resolve<IJsonSerializer>();
            var group = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.First(g => g.Id == groupId);
            this.currentName = group.Name;
            this.ContentData = new RenameGroupUI { Name = group.Name };
            this.AllowOk = true;
            this.AllowCancel = true;
        }
        public override string Caption => "Rename: " + this.currentName;
        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            this.logger.Info("RenameGroupDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.currentName, this.groupId);
            if (string.Equals(commandId, GroupsCommands.ConfirmRename, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ui = this.serializer.DeserializeFromString<RenameGroupUI>(data) ?? new RenameGroupUI();
                    var name = ui.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        ui.Name = this.currentName;
                        ui.ValidationStatus = new CaptionItem("✗ A group name is required");
                        this.ContentData = ui;
                        this.RaiseUIViewInfoChanged();
                        return Task.FromResult<IPluginUIView>(this);
                    }

                    var renamed = false;
                    Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    {
                        if (s.Groups.Any(g => g.Id != this.groupId && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))) return;
                        var group = s.Groups.FirstOrDefault(g => g.Id == this.groupId);
                        if (group == null) return;
                        group.Name = name;
                        renamed = true;
                    }).GetAwaiter().GetResult();

                    if (renamed)
                    {
                        this.logger.Info("Renamed group '{0}' ({1}) to '{2}'", this.currentName, this.groupId, name);
                        this.rebuildParentContent();
                        return Task.FromResult(this.parentPageView);
                    }

                    this.logger.Info("Rename target '{0}' already exists", name);
                    ui.ValidationStatus = new CaptionItem($"✗ A group named '{name}' already exists");
                    this.ContentData = ui;
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("Group rename dialog failed", ex);
                    return Task.FromResult<IPluginUIView>(this);
                }
            }

            if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandId, "DialogOk", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("Group rename dialog closed for '{0}' ({1})", this.currentName, this.groupId);
                this.rebuildParentContent();
                return Task.FromResult(this.parentPageView);
            }

            return base.RunCommand(itemId, commandId, data);
        }
        public override Task Cancel() => Task.CompletedTask;
    }

    public class DeleteGroupUI : EditableOptionsBase
    {
        [Browsable(false)]
        public string Title { get; set; }
        public override string EditorTitle => null;
        public override string EditorDescription => null;
        public CaptionItem ImpactNotice { get; set; } = new CaptionItem("This group is currently assigned to the following users. Deleting it removes the group from their recommendation access settings; it does not delete the users.");
        public GenericItemList CurrentMembers { get; set; } = new GenericItemList();
        [DisplayName("Group name confirmation")]
        [Description("Enter the group name to confirm deletion.")]
        public string Confirmation { get; set; } = string.Empty;
        public ButtonItem DeleteButton { get; set; } = new ButtonItem("Delete Group")
        {
            StandardIcon = StandardIcons.Remove,
            CommandId = GroupsCommands.ConfirmDelete
        };
        public CaptionItem ValidationStatus { get; set; } = new CaptionItem(string.Empty);
    }

    internal class DeleteGroupDialogView : PluginDialogView
    {
        private readonly string groupId;
        private readonly string groupName;
        private readonly IJsonSerializer serializer;
        private readonly IPluginUIView parentPageView;
        private readonly Action rebuildParentContent;
        private readonly ILogger logger;
        public DeleteGroupDialogView(
            string pluginId,
            string groupId,
            IPluginUIView parentPageView,
            Action rebuildParentContent,
            IServerApplicationHost host,
            ILogger logger) : base(pluginId)
        {
            this.groupId = groupId;
            this.parentPageView = parentPageView;
            this.rebuildParentContent = rebuildParentContent;
            this.logger = logger;
            this.serializer = host.Resolve<IJsonSerializer>();
            var group = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.First(g => g.Id == groupId);
            this.groupName = group.Name;
            this.ContentData = this.BuildContent(group.MemberUserIds.ToArray());
            this.AllowOk = true;
            this.AllowCancel = true;
        }
        public override string Caption => "Delete: " + this.groupName;
        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;

        private DeleteGroupUI BuildContent(long[] memberUserIds, string validationMessage = null)
        {
            var memberIds = memberUserIds ?? Array.Empty<long>();
            return new DeleteGroupUI
            {
                Title = $"Delete {this.groupName}",
                CurrentMembers = new GenericItemList(Plugin.Instance.GetAllUsers()
                    .Where(user => memberIds.Contains(user.InternalId))
                    .OrderBy(user => user.Name)
                    .Select(user => new GenericListItem
                    {
                        PrimaryText = user.Name,
                        SecondaryText = "Current member",
                        Icon = IconNames.person
                    })),
                ValidationStatus = new CaptionItem(validationMessage ?? string.Empty)
            };
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            this.logger.Info("DeleteGroupDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.groupName, this.groupId);
            if (string.Equals(commandId, GroupsCommands.ConfirmDelete, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ui = this.serializer.DeserializeFromString<DeleteGroupUI>(data);
                    if (ui != null && string.Equals(ui.Confirmation?.Trim(), this.groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.Groups.RemoveAll(g => g.Id == this.groupId)).GetAwaiter().GetResult();
                        this.logger.Info("Deleted group '{0}' ({1})", this.groupName, this.groupId);
                        this.rebuildParentContent();
                        return Task.FromResult(this.parentPageView);
                    }

                    this.logger.Info("Delete confirmation did not match group '{0}'", this.groupName);
                    var group = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.FirstOrDefault(g => g.Id == this.groupId);
                    this.ContentData = this.BuildContent(group?.MemberUserIds.ToArray(), "✗ Name did not match — try again");
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("Group delete dialog failed", ex);
                    return Task.FromResult<IPluginUIView>(this);
                }
            }

            if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase)
                || string.Equals(commandId, "DialogOk", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("Group delete dialog closed for '{0}' ({1})", this.groupName, this.groupId);
                this.rebuildParentContent();
                return Task.FromResult(this.parentPageView);
            }

            return base.RunCommand(itemId, commandId, data);
        }
        public override Task Cancel() => Task.CompletedTask;
    }
}
