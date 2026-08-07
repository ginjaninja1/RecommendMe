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

            if (commandId == GroupsCommands.Create && !string.IsNullOrWhiteSpace(state.NewGroupName))
            {
                var name = state.NewGroupName.Trim();
                var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
                if (settings.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    state.StatusMessage = Recommend.RecommendViewBuilder.BuildStatusMessage($"A group named '{name}' already exists.", false);
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
                    state.StatusMessage = created
                        ? Recommend.RecommendViewBuilder.BuildStatusMessage($"Created group '{name}'.", true)
                        : Recommend.RecommendViewBuilder.BuildStatusMessage($"A group named '{name}' already exists.", false);
                    if (created)
                    {
                        state.NewGroupName = string.Empty;
                        changed = true;
                    }
                }
            }
            else if (GroupsCommands.TryMembers(commandId, out var membersGroupId))
            {
                return Task.FromResult<IPluginUIView>(new GroupMembersDialogView(this.PluginId, membersGroupId, this.applicationHost));
            }
            else if (GroupsCommands.TryRename(commandId, out var renameGroupId))
            {
                return Task.FromResult<IPluginUIView>(new RenameGroupDialogView(this.PluginId, renameGroupId, this.applicationHost));
            }
            else if (GroupsCommands.TryDelete(commandId, out var deleteGroupId))
            {
                return Task.FromResult<IPluginUIView>(new DeleteGroupDialogView(
                    this.PluginId,
                    deleteGroupId,
                    this,
                    () => this.Rebuild((GroupsUI)this.ContentData),
                    this.applicationHost,
                    this.logger));
            }

            if (changed) this.logger.Info("RecommendMe: groups updated (command '{0}')", commandId);
            this.Rebuild(state);
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }

        public override void OnDialogResult(IPluginUIView dialogView, bool completedOk, object data)
        {
            this.Rebuild((GroupsUI)this.ContentData);
            this.RaiseUIViewInfoChanged();
        }
    }
}
