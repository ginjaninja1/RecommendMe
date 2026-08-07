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
        public string Title { get; set; }
        public override string EditorTitle => Title;
        public override string EditorDescription => "Search for a user, then add or remove them from this group.";
        [DisplayName("Username search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;
        public GenericItemList UserResults { get; set; } = new GenericItemList();
    }

    internal class GroupMembersDialogView : PluginDialogView
    {
        private readonly string groupId;
        private readonly IJsonSerializer serializer;
        public GroupMembersDialogView(string pluginId, string groupId, IServerApplicationHost host) : base(pluginId)
        {
            this.groupId = groupId;
            this.serializer = host.Resolve<IJsonSerializer>();
            this.AllowOk = true;
            this.Rebuild(new GroupMembersUI());
        }

        private void Rebuild(GroupMembersUI state)
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            var group = settings.Groups.FirstOrDefault(g => g.Id == this.groupId);
            state.Title = group == null ? "Group Members" : $"Members of {group.Name}";
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
            var state = string.IsNullOrEmpty(data) ? (GroupMembersUI)this.ContentData : this.serializer.DeserializeFromString<GroupMembersUI>(data) ?? new GroupMembersUI();
            if (GroupsCommands.TryToggleUser(commandId, out var parsedGroupId, out var userId) && parsedGroupId == this.groupId)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == this.groupId);
                    if (group == null) return;
                    if (group.MemberUserIds.Contains(userId)) group.MemberUserIds.Remove(userId); else group.MemberUserIds.Add(userId);
                }).GetAwaiter().GetResult();
            }
            this.Rebuild(state);
            return Task.FromResult<IPluginUIView>(this);
        }

        public override Task OnOkCommand(string providerId, string commandId, string data) => Task.CompletedTask;
    }

    public class RenameGroupUI : EditableOptionsBase
    {
        public string Title { get; set; }
        public override string EditorTitle => Title;
        public override string EditorDescription => "Enter a unique group name and save.";
        [DisplayName("Group name")]
        [AutoPostBack(GroupsCommands.ValidateRename, nameof(Name))]
        public string Name { get; set; }
        public GenericItemList StatusMessage { get; set; } = new GenericItemList();
    }

    internal class RenameGroupDialogView : PluginDialogView
    {
        private readonly string groupId;
        private readonly IJsonSerializer serializer;
        public RenameGroupDialogView(string pluginId, string groupId, IServerApplicationHost host) : base(pluginId)
        {
            this.groupId = groupId;
            this.serializer = host.Resolve<IJsonSerializer>();
            var group = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups.First(g => g.Id == groupId);
            this.ContentData = new RenameGroupUI { Title = $"Rename {group.Name}", Name = group.Name };
            this.OKButtonCaption = "Save";
        }
        public override Task OnOkCommand(string providerId, string commandId, string data)
        {
            var ui = this.serializer.DeserializeFromString<RenameGroupUI>(data) ?? (RenameGroupUI)this.ContentData;
            var name = ui.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return Task.CompletedTask;
            Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
            {
                if (s.Groups.Any(g => g.Id != this.groupId && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))) return;
                var group = s.Groups.FirstOrDefault(g => g.Id == this.groupId);
                if (group != null) group.Name = name;
            }).GetAwaiter().GetResult();
            return Task.CompletedTask;
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var ui = this.serializer.DeserializeFromString<RenameGroupUI>(data) ?? (RenameGroupUI)this.ContentData;
            var name = ui.Name?.Trim();
            var duplicate = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult().Groups
                .Any(g => g.Id != this.groupId && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            ui.StatusMessage = string.IsNullOrWhiteSpace(name)
                ? Recommend.RecommendViewBuilder.BuildStatusMessage("A group name is required.", false)
                : duplicate
                    ? Recommend.RecommendViewBuilder.BuildStatusMessage($"A group named '{name}' already exists.", false)
                    : Recommend.RecommendViewBuilder.BuildStatusMessage("This group name is available.", true);
            this.ContentData = ui;
            return Task.FromResult<IPluginUIView>(this);
        }
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
