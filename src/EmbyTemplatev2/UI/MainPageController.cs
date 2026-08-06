using MediaBrowser.Controller;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using EmbyTemplatev2.UIBaseClasses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmbyTemplatev2.UI
{
    internal class MainPageController : ControllerBase, IHasTabbedUIPages
    {
        private readonly PluginInfo pluginInfo;
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        private readonly List<IPluginUIPageController> tabPages =
            new List<IPluginUIPageController>();


        public MainPageController(
            PluginInfo pluginInfo,
            IServerApplicationHost applicationHost,
            ILogger logger)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.applicationHost = applicationHost;
            this.logger = logger;

            this.PageInfo = new PluginPageInfo
            {
                Name = "EmbyTemplatev2",
                EnableInMainMenu = false,
                DisplayName = "Poster To Folder",
                MenuIcon = "image",
                IsMainConfigPage = true
            };
        }


        public override PluginPageInfo PageInfo { get; }


        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view =
                new ConfigPageView(
                    this.pluginInfo,
                    this.applicationHost,
                    this.logger);

            return Task.FromResult(view);
        }


        public IReadOnlyList<IPluginUIPageController> TabPageControllers =>
            this.tabPages.AsReadOnly();
    }
}