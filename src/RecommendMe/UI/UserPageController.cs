namespace RecommendMe.UI
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MediaBrowser.Controller;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Plugins;
    using MediaBrowser.Model.Plugins.UI;
    using MediaBrowser.Model.Plugins.UI.Views;
    using RecommendMe.UI.Account;
    using RecommendMe.UI.History;
    using RecommendMe.UI.Recommend;
    using RecommendMe.UIBaseClasses;

    /// <summary>
    /// Ordinary-user entry point: Recommend (default), History, and Account tabs.
    /// EnableInUserMenu = true puts this in every user's own menu, separate
    /// from the admin-only <see cref="AdminPageController"/> entry. Neither
    /// tab is admin-gated - see AdminPageController for the admin-only page.
    /// </summary>
    internal class UserPageController : ControllerBase, IHasTabbedUIPages
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;
        private readonly List<IPluginUIPageController> tabPages;

        public UserPageController(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.applicationHost = applicationHost;
            this.logger = logger;

            this.PageInfo = new PluginPageInfo
            {
                Name = "RecommendMe",
                EnableInMainMenu = false,
                EnableInUserMenu = true,
                DisplayName = "RecommendMe",
                MenuIcon = "recommend",
                IsMainConfigPage = false
            };

            this.tabPages = new List<IPluginUIPageController>
            {
                new TabPageController(
                    pluginInfo,
                    "RecommendMeHistory",
                    "History",
                    info => new HistoryPageView(info, this.applicationHost)),
                new TabPageController(
                    pluginInfo,
                    "RecommendMeAccount",
                    "Receive Policy",
                    info => new AccountPageView(info, this.logger))
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public IReadOnlyList<IPluginUIPageController> TabPageControllers => this.tabPages.AsReadOnly();

        // Tab 1 (default): Recommend
        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new RecommendPageView(this.pluginInfo, this.applicationHost, this.logger);
            return Task.FromResult(view);
        }
    }
}
