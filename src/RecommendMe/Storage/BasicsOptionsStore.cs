
using MediaBrowser.Common;
using MediaBrowser.Model.Logging;
using RecommendMe.UI.Config;
using RecommendMe.UIBaseClasses.Store;

namespace RecommendMe.Storage
{
    public class BasicsOptionsStore : SimpleFileStore<ConfigUI>
    {
        public BasicsOptionsStore(IApplicationHost applicationHost, ILogger logger, string pluginFullName)
        : base(applicationHost, logger, pluginFullName)
        {
        }
    }
}
