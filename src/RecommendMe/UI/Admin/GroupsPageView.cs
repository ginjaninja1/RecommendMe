using System;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Serialization;
using RecommendMe.Models;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    internal class GroupsPageView : PluginPageView
    {
        private readonly IJsonSerializer jsonSerializer;
        private readonly ILogger logger;
        private readonly IServerApplicationHost applicationHost;

        public GroupsPageView(PluginInfo pluginInfo, IServerApplicationHost host, ILogger logger) : base(pluginInfo.Id)
        {
            this.jsonSerializer = host.Resolve<IJsonSerializer>();
            this.applicationHost = host;
            this.logger = logger;
            this.ShowSave = false;
            this.ShowBack = false;
            this.Rebuild(new GroupsUI());
        }

        private void Rebuild(GroupsUI state)
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            this.ContentData = GroupsViewBuilder.Build(settings, Plugin.Instance.GetAllUsers(), state);
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            var state = string.IsNullOrEmpty(data) ? (GroupsUI)this.ContentData : this.jsonSerializer.DeserializeFromString<GroupsUI>(data) ?? new GroupsUI();
            var changed = false;

            if (commandId == GroupsCommands.Create)
            {
                var name = state.NewGroupName?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    SetCreateStatus(state, "A group name is required.", false);
                    this.Rebuild(state);
                    this.RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);
                }
                var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
                if (settings.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    SetCreateStatus(state, $"A group named '{name}' already exists.", false);
                }
                else
                {
                    var created = false;
                    Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    {
                        if (s.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))) return;
                        s.Groups.Add(new UserGroup { Name = name });
                        created = true;
                    }).GetAwaiter().GetResult();
                    SetCreateStatus(
                        state,
                        created ? $"Created group '{name}'." : $"A group named '{name}' already exists.",
                        created);
                    if (created)
                    {
                        state.NewGroupName = string.Empty;
                        changed = true;
                    }
                }
            }
            else if (GroupsCommands.TryMembers(commandId, out var membersGroupId))
            {
                return Task.FromResult<IPluginUIView>(new GroupMembersDialogView(
                    this.PluginId,
                    membersGroupId,
                    this,
                    () => this.Rebuild((GroupsUI)this.ContentData),
                    this.applicationHost,
                    this.logger));
            }
            else if (GroupsCommands.TryRename(commandId, out var renameGroupId))
            {
                return Task.FromResult<IPluginUIView>(new RenameGroupDialogView(
                    this.PluginId,
                    renameGroupId,
                    this,
                    () => this.Rebuild((GroupsUI)this.ContentData),
                    this.applicationHost,
                    this.logger));
            }
            else if (GroupsCommands.TryDelete(commandId, out var deleteGroupId))
            {
                var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
                var group = settings.Groups.FirstOrDefault(g => g.Id == deleteGroupId);
                if (group != null && group.MemberUserIds.Count == 0)
                {
                    var deleted = false;
                    var nowHasMembers = false;
                    Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                    {
                        var currentGroup = s.Groups.FirstOrDefault(g => g.Id == deleteGroupId);
                        if (currentGroup == null)
                        {
                            deleted = true;
                            return;
                        }
                        if (currentGroup.MemberUserIds.Count != 0)
                        {
                            nowHasMembers = true;
                            return;
                        }
                        deleted = s.Groups.Remove(currentGroup);
                    }).GetAwaiter().GetResult();

                    if (deleted)
                    {
                        this.logger.Info("Deleted empty group '{0}' ({1}) without confirmation", group.Name, group.Id);
                        changed = true;
                    }
                    else if (nowHasMembers)
                    {
                        return Task.FromResult<IPluginUIView>(new DeleteGroupDialogView(
                            this.PluginId,
                            deleteGroupId,
                            this,
                            () => this.Rebuild((GroupsUI)this.ContentData),
                            this.applicationHost,
                            this.logger));
                    }
                }
                else if (group != null)
                {
                    return Task.FromResult<IPluginUIView>(new DeleteGroupDialogView(
                        this.PluginId,
                        deleteGroupId,
                        this,
                        () => this.Rebuild((GroupsUI)this.ContentData),
                        this.applicationHost,
                        this.logger));
                }
            }

            if (changed) this.logger.Info("Groups updated (command '{0}')", commandId);
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }

        public override void OnDialogResult(IPluginUIView dialogView, bool completedOk, object data)
        {
            this.Rebuild((GroupsUI)this.ContentData);
            this.RaiseUIViewInfoChanged();
        }

        private static void SetCreateStatus(GroupsUI state, string message, bool success)
        {
            if (state.CreateAction == null || state.CreateAction.Count == 0)
            {
                state.CreateAction = new GroupsUI().CreateAction;
            }

            state.CreateAction[0].SecondaryText = message;
            state.CreateAction[0].Status = success ? ItemStatus.Succeeded : ItemStatus.Failed;
        }
    }
}
