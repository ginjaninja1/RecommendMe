using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;
using System.Collections.Generic;
using System.ComponentModel;

namespace EmbyTemplatev2.UI.Config
{
    public class ConfigUI : EditableOptionsBase
    {
        public override string EditorTitle => "Poster To Folder - Configuration";

        public override string EditorDescription =>
            "Copies each movie/show's poster image to folder.ext on disk when a folder image is missing.";

        public CaptionItem GeneralHeading { get; set; } = new CaptionItem("General");

        [DisplayName("Enable Plugin")]
        [Description("When disabled, the scheduled task exits immediately without processing any items.")]
        [AutoPostBack("updateconfig", nameof(EnablePlugin))]
        public bool EnablePlugin { get; set; } = true;

        public CaptionItem LibraryFilterHeading { get; set; } =
            new CaptionItem("Library / Path Filter");


        /// <summary>
        /// Persistent configuration data.
        /// </summary>
        [Browsable(false)]
        public List<LibraryPathFilterItem> LibraryPaths { get; set; } =
            new List<LibraryPathFilterItem>();


        /// <summary>
        /// GenericUI representation of LibraryPaths.
        /// </summary>
        public GenericItemList LibraryList { get; set; } =
            new GenericItemList();

        /*
        public GenericItemList ScheduledTaskLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Configure Scheduled Task",
                SecondaryText = "",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "/scheduledtasks",
                HyperLinkTargetExternal = true
            }
        };
        */
        public GenericItemList ScheduledTaskLink { get; set; } = new GenericItemList();

        public GenericItemList ForumLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Community Forum",
                SecondaryText = "Issues, Suggestions and Updates",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://emby.media/community/topic/148589-plugin-poster-to-folder/",
                HyperLinkTargetExternal = true
            }
        };

        public GenericItemList GithubLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Github repository",
                SecondaryText = "",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                HyperLink = "https://github.com/ginjaninja1/EmbyTemplatev2",
                HyperLinkTargetExternal = true
            }
        };
    }
}