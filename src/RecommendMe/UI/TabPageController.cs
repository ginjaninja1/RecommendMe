namespace RecommendMe.UI
{
    using System;
    using System.Threading.Tasks;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Plugins;
    using MediaBrowser.Model.Plugins.UI;
    using MediaBrowser.Model.Plugins.UI.Views;
    using RecommendMe.UI.Security;
    using RecommendMe.UIBaseClasses;

    /// <summary>Simple tab page controller that uses a factory function to create the view.</summary>
    internal class TabPageController : ControllerBase, IPluginPageSecurity
    {
        private readonly PluginInfo pluginInfo;
        private readonly Func<PluginInfo, IPluginUIView> factoryFunc;
        private readonly bool adminOnly;

        public TabPageController(
            PluginInfo pluginInfo,
            string name,
            string displayName,
            Func<PluginInfo, IPluginUIView> factoryFunc,
            bool adminOnly = false)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.factoryFunc = factoryFunc;
            this.adminOnly = adminOnly;
            this.PageInfo = new PluginPageInfo { Name = name, DisplayName = displayName };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            var view = this.factoryFunc(this.pluginInfo);
            return Task.FromResult(view);
        }

        // IPluginPageSecurity - each tab is registered independently of its
        // parent controller, so admin-gated tabs must implement this
        // themselves; gating only the parent leaves the tab reachable
        // directly by its pageId. See AdminOnlyPageSecurity for details.
        public Task CheckIsUserAuthorised(UserDto user, IPluginUIView requestedView)
        {
            return this.adminOnly
                ? AdminOnlyPageSecurity.CheckIsAdmin(user, requestedView)
                : Task.CompletedTask;
        }
    }
}
