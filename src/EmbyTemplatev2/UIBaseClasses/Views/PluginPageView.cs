using MediaBrowser.Model.Plugins.UI.Views;
using System.Threading.Tasks;

namespace EmbyTemplatev2.UIBaseClasses.Views
{
    internal abstract class PluginPageView : PluginViewBase, IPluginPageView
    {
        protected PluginPageView(string pluginId)
        : base(pluginId)
        {
        }

        public bool ShowSave { get; set; } = true;

        public bool ShowBack { get; set; } = false;

        public bool AllowSave { get; set; } = true;

        public bool AllowBack { get; set; } = true;

        public virtual Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            return Task.FromResult((IPluginUIView)this);
        }
    }
}
