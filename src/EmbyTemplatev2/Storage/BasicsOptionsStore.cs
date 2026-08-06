
using MediaBrowser.Common;
using MediaBrowser.Model.Logging;
using EmbyTemplatev2.UI.Config;
using EmbyTemplatev2.UIBaseClasses.Store;

namespace EmbyTemplatev2.Storage
{
    public class BasicsOptionsStore : SimpleFileStore<ConfigUI>
    {
        public BasicsOptionsStore(IApplicationHost applicationHost, ILogger logger, string pluginFullName)
        : base(applicationHost, logger, pluginFullName)
        {
        }
    }
}
