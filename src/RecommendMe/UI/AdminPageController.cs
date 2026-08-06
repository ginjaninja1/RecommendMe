namespace RecommendMe.UI
{
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
    internal class AdminPageController : ControllerBase, IPluginPageSecurity
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

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
        }

        public override PluginPageInfo PageInfo { get; }

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
