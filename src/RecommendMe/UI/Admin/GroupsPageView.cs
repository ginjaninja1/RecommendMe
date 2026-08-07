using System;
using System.Linq;
using System.Threading.Tasks;
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

        public GroupsPageView(PluginInfo pluginInfo, IServerApplicationHost host, ILogger logger) : base(pluginInfo.Id)
        {
            this.jsonSerializer = host.Resolve<IJsonSerializer>();
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

            if (commandId == GroupsCommands.Create && !string.IsNullOrWhiteSpace(state.NewGroupName))
            {
                var group = new UserGroup { Name = state.NewGroupName.Trim() };
                Plugin.Instance.AdminSettingsStore.MutateAsync(s => s.Groups.Add(group)).GetAwaiter().GetResult();
                state.SelectedGroupId = group.Id;
                state.NewGroupName = string.Empty;
                changed = true;
            }
            else if (GroupsCommands.TrySelect(commandId, out var selectedId))
            {
                state.SelectedGroupId = selectedId;
            }
            else if (commandId == GroupsCommands.Rename && !string.IsNullOrWhiteSpace(state.RenameName))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == state.SelectedGroupId);
                    if (group != null) group.Name = state.RenameName.Trim();
                }).GetAwaiter().GetResult();
                changed = true;
            }
            else if (commandId == GroupsCommands.Delete)
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == state.SelectedGroupId);
                    if (group != null && group.MemberUserIds.Count == 0) s.Groups.Remove(group);
                }).GetAwaiter().GetResult();
                state.SelectedGroupId = null;
                changed = true;
            }
            else if (GroupsCommands.TryToggleUser(commandId, out var groupId, out var userId))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    var group = s.Groups.FirstOrDefault(g => g.Id == groupId);
                    if (group == null) return;
                    if (group.MemberUserIds.Contains(userId)) group.MemberUserIds.Remove(userId);
                    else group.MemberUserIds.Add(userId);
                }).GetAwaiter().GetResult();
                changed = true;
            }

            if (changed) this.logger.Info("RecommendMe: groups updated (command '{0}')", commandId);
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }
    }
}
