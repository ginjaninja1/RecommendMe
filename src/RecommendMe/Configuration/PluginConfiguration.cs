using MediaBrowser.Model.Plugins;

namespace RecommendMe.Configuration
{
    /// <summary>
    /// Emby's BasePlugin&lt;T&gt; requires a configuration type, but
    /// RecommendMe deliberately does NOT use it for real settings: admin
    /// settings, recommendation history, and user preferences are all
    /// persisted as JSON under ProgramData\data\RecommendMe\ instead (see
    /// RecommendMe.Storage). This class is therefore intentionally near-empty -
    /// it exists only to satisfy BasePlugin&lt;T&gt;.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
    }
}
