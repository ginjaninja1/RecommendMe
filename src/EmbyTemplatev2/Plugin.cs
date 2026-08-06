using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;
using EmbyTemplatev2.Configuration;
using EmbyTemplatev2.UI;
using System;
using System.Collections.Generic;
using System.IO; 

namespace EmbyTemplatev2
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasUIPages
    {
        private readonly IServerApplicationHost applicationHost;
        private readonly ILogger logger;

        private List<IPluginUIPageController> pages;


        public Plugin(
            IServerApplicationHost applicationHost,
            ILogManager logManager)
            : base(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IXmlSerializer>())
        {
            this.applicationHost = applicationHost;

            // Create the plugin logger once.
            this.logger = logManager.GetLogger(this.Name);

            Instance = this;
        }


        /// <summary>
        /// Gets the running instance of this plugin. Configuration is
        /// accessed via Instance.Configuration / SaveConfiguration() /
        /// UpdateConfiguration() - inherited from BasePlugin&lt;T&gt;, no
        /// custom store needed.
        /// </summary>
        public static Plugin Instance { get; private set; }


        public override string Description =>
            "Copies poster.ext to folder.ext for movies and TV shows that are missing a folder image.";


        public override Guid Id =>
            new Guid("1E0C5960-DF19-4C22-AF9A-FA0FDC3EF649");


        public override string Name =>
            "Poster To Folder";


        public ImageFormat ThumbImageFormat =>
            ImageFormat.Png;


        public Stream GetThumbImage()
            => this.GetType()
                .Assembly
                .GetManifestResourceStream(
                    this.GetType().Namespace + ".thumb.png");


        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (this.pages == null)
                {
                    this.pages = new List<IPluginUIPageController>();

                    this.pages.Add(
                        new MainPageController(
                            this.GetPluginInfo(),
                            this.applicationHost,
                            this.logger));
                }

                return this.pages.AsReadOnly();
            }
        }
    }
}