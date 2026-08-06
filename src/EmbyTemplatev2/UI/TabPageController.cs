using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using EmbyTemplatev2.UIBaseClasses;
using System;
using System.Threading.Tasks;

namespace EmbyTemplatev2.UI
{
    internal class TabPageController : ControllerBase
    {
        private readonly PluginInfo pluginInfo;
        private readonly Func<PluginInfo, IPluginUIView> factoryFunc;

        public TabPageController(PluginInfo pluginInfo, string name, string displayName, Func<PluginInfo, IPluginUIView> factoryFunc)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.factoryFunc = factoryFunc;
            this.PageInfo = new PluginPageInfo
            {
                Name = name,
                DisplayName = displayName,
            };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            var view = this.factoryFunc(this.pluginInfo);
            return Task.FromResult(view);
        }
    }
}
