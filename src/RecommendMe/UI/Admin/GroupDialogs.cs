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
        public string UserSearch { get; set; } = string.Empty;
        public GenericItemList UserResults { get; set; } = new GenericItemList();
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
            this.AllowOk = false;
            this.AllowCancel = true;
            this.Rebuild(new GroupMembersUI());
        }

        public override string Caption => "Add/remove users: " + this.groupName;

        private void Rebuild(GroupMembersUI state)
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            var group = settings.Groups.FirstOrDefault(g => g.Id == this.groupId);
            state.UserResults = new GenericItemList();
            if (group != null && !string.IsNullOrWhiteSpace(state.UserSearch))
            {
                foreach (var user in Plugin.Instance.GetAllUsers().Where(u => u.Name.IndexOf(state.UserSearch, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(u => u.Name).Take(10))
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
            this.logger.Info("RecommendMe: GroupMembersDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.groupName, this.groupId);
            var state = string.IsNullOrEmpty(data) ? (GroupMembersUI)this.ContentData : this.serializer.DeserializeFromString<GroupMembersUI>(data) ?? new GroupMembersUI();
            if (GroupsCommands.TryToggleUser(commandId, out var parsedGroupId, out var userId) && parsedGroupId == this.groupId)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == this.groupId);
                    if (group == null) return;
                    if (group.MemberUserIds.Contains(userId)) group.MemberUserIds.Remove(userId); else group.MemberUserIds.Add(userId);
                }).GetAwaiter().GetResult();
                this.logger.Info("RecommendMe: toggled user {0} membership in group '{1}' ({2})", userId, this.groupName, this.groupId);
            }
            else if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("RecommendMe: group membership dialog closed for '{0}' ({1})", this.groupName, this.groupId);
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
            this.AllowOk = false;
            this.AllowCancel = true;
        }
        public override string Caption => "Rename: " + this.currentName;
        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            this.logger.Info("RecommendMe: RenameGroupDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.currentName, this.groupId);
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
                        this.logger.Info("RecommendMe: renamed group '{0}' ({1}) to '{2}'", this.currentName, this.groupId, name);
                        this.rebuildParentContent();
                        return Task.FromResult(this.parentPageView);
                    }

                    this.logger.Info("RecommendMe: rename target '{0}' already exists", name);
                    ui.ValidationStatus = new CaptionItem($"✗ A group named '{name}' already exists");
                    this.ContentData = ui;
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("RecommendMe: group rename dialog failed", ex);
                    return Task.FromResult<IPluginUIView>(this);
                }
            }

            if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("RecommendMe: group rename cancelled for '{0}' ({1})", this.currentName, this.groupId);
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
        [DisplayName("Group name confirmation")]
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
            this.groupName = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.First(g => g.Id == groupId).Name;
            this.ContentData = new DeleteGroupUI { Title = $"Delete {this.groupName}" };
            this.AllowOk = false;
            this.AllowCancel = true;
        }
        public override string Caption => "Delete: " + this.groupName;
        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;
        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            this.logger.Info("RecommendMe: DeleteGroupDialog command '{0}' for group '{1}' ({2})", commandId ?? "(null)", this.groupName, this.groupId);
            if (string.Equals(commandId, GroupsCommands.ConfirmDelete, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var ui = this.serializer.DeserializeFromString<DeleteGroupUI>(data);
                    if (ui != null && string.Equals(ui.Confirmation?.Trim(), this.groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.Groups.RemoveAll(g => g.Id == this.groupId)).GetAwaiter().GetResult();
                        this.logger.Info("RecommendMe: deleted group '{0}' ({1})", this.groupName, this.groupId);
                        this.rebuildParentContent();
                        return Task.FromResult(this.parentPageView);
                    }

                    this.logger.Info("RecommendMe: delete confirmation did not match group '{0}'", this.groupName);
                    this.ContentData = new DeleteGroupUI
                    {
                        Title = $"Delete {this.groupName}",
                        ValidationStatus = new CaptionItem("✗ Name did not match — try again")
                    };
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
                }
                catch (Exception ex)
                {
                    this.logger.ErrorException("RecommendMe: group delete dialog failed", ex);
                    return Task.FromResult<IPluginUIView>(this);
                }
            }

            if (string.Equals(commandId, "DialogCancel", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.Info("RecommendMe: group delete cancelled for '{0}' ({1})", this.groupName, this.groupId);
                this.rebuildParentContent();
                return Task.FromResult(this.parentPageView);
            }

            return base.RunCommand(itemId, commandId, data);
        }
        public override Task Cancel() => Task.CompletedTask;
    }
}
