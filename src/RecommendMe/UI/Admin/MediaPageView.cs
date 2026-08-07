using System.Threading.Tasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using RecommendMe.UIBaseClasses.Views;

namespace RecommendMe.UI.Admin
{
    internal class MediaPageView : PluginPageView
    {
        private readonly ILogger logger;
        public MediaPageView(PluginInfo pluginInfo, ILogger logger) : base(pluginInfo.Id)
        {
            this.logger = logger;
            this.ShowSave = false;
            this.ShowBack = false;
            this.Rebuild();
        }

        private void Rebuild()
        {
            var settings = Plugin.Instance.AdminSettingsStore.GetAsync().GetAwaiter().GetResult();
            this.ContentData = new MediaUI { MediaTypeList = AdminViewBuilder.BuildMediaTypeList(settings.GloballyAllowedMediaTypes) };
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (AdminCommands.TryParseMediaType(commandId, out var mediaType))
            {
                Plugin.Instance.AdminSettingsStore.MutateAsync(s =>
                {
                    if (s.GloballyAllowedMediaTypes.Contains(mediaType)) s.GloballyAllowedMediaTypes.Remove(mediaType);
                    else s.GloballyAllowedMediaTypes.Add(mediaType);
                }).GetAwaiter().GetResult();
                this.logger.Info("Media permission updated for {0}", mediaType);
            }
            this.Rebuild();
            this.RaiseUIViewInfoChanged();
            return Task.FromResult<IPluginUIView>(this);
        }
    }
}
