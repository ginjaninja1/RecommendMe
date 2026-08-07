namespace RecommendMe.UI
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MediaBrowser.Controller;
    using MediaBrowser.Model.Dto;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Plugins;
    using MediaBrowser.Model.Plugins.UI;
    using MediaBrowser.Model.Plugins.UI.Views;
    using RecommendMe.UI.Admin;
    using RecommendMe.UI.Security;
    using RecommendMe.UIBaseClasses;

    /// <summary>
    /// Admin entry point: RecommendMe's permission-matrix settings. This is
    /// the plugin's "main config page" (what an admin sees when they click
    /// RecommendMe in Dashboard &gt; Plugins) and is gated admin-only via
    /// IPluginPageSecurity - see UI/Security/AdminOnlyPageSecurity.cs for why
    /// that's required (menu-visibility flags alone don't enforce anything).
    /// The ordinary-user-facing entry point is <see cref="UserPageController"/>.
    /// </summary>
    internal class AdminPageController : ControllerBase, IPluginPageSecurity, IHasTabbedUIPages
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;
        private readonly List<IPluginUIPageController> tabPages;

        public AdminPageController(PluginInfo pluginInfo, IServerApplicationHost applicationHost, ILogger logger)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.applicationHost = applicationHost;
            this.logger = logger;

            this.PageInfo = new PluginPageInfo
            {
                Name = "RecommendMeAdmin",
                DisplayName = "RecommendMe",
                EnableInMainMenu = true,
                EnableInUserMenu = false,
                MenuIcon = "recommend",
                IsMainConfigPage = true
            };

            this.tabPages = new List<IPluginUIPageController>
            {
                new TabPageController(pluginInfo, "RecommendMeGroups", "Groups", info => new GroupsPageView(info, applicationHost, logger), true),
                new TabPageController(pluginInfo, "RecommendMeMedia", "Media", info => new MediaPageView(info, logger), true),
                new TabPageController(pluginInfo, "RecommendMeCollectionSettings", "Config", info => new CollectionSettingsPageView(info, applicationHost, logger), true)
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public IReadOnlyList<IPluginUIPageController> TabPageControllers => this.tabPages.AsReadOnly();

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new AdminPageView(this.pluginInfo, this.applicationHost, this.logger);
            return Task.FromResult(view);
        }

        public Task CheckIsUserAuthorised(UserDto user, IPluginUIView requestedView)
        {
            return AdminOnlyPageSecurity.CheckIsAdmin(user, requestedView);
        }
    }
}
