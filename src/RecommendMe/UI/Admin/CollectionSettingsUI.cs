using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;

namespace RecommendMe.UI.Admin
{
    public class CollectionSettingsUI : EditableOptionsBase
    {
        public override string EditorTitle => "Config";
        public override string EditorDescription => "Configure RecommendMe behaviour and collection settings.";

        public CaptionItem ExpandUserBehaviourHeading { get; set; } = new CaptionItem("Expand User Behaviour");

        [DisplayName("Always Expand Users and Groups")]
        [Description("On: show every user and group and allow the page to scroll. Off: show search filters and limit each result list to 10 items.")]
        [AutoPostBack(CollectionSettingsCommands.SaveExpansionSetting, nameof(AlwaysExpandUsersAndGroups))]
        public bool AlwaysExpandUsersAndGroups { get; set; } = true;

        public CaptionItem CleanupHeading { get; set; } = new CaptionItem("Watched Recommendations");

        [DisplayName("Clear watched recommendations")]
        [Description("When enabled, watched recommendations are removed on playback and by the user-configurable RecommendMe scheduled task.")]
        [AutoPostBack(CollectionSettingsCommands.SaveWatchedSettings, nameof(ClearWatchedRecommendations))]
        public bool ClearWatchedRecommendations { get; set; }

        [DisplayName("Prevent Watched Recommendations")]
        [Description("When enabled, an item cannot be recommended to a user who has already watched it. Items without a watched status can still be recommended.")]
        [AutoPostBack(CollectionSettingsCommands.SaveWatchedSettings, nameof(PreventWatchedRecommendations))]
        public bool PreventWatchedRecommendations { get; set; }

        public CaptionItem NamingHeading { get; set; } = new CaptionItem("Collection Naming");

        [DisplayName("Recommendation Collection Prefix")]
        public string RecommendationCollectionPrefix { get; set; } = Models.AdminSettings.DefaultRecommendationCollectionPrefix;

        [DisplayName("Recommendation Collection Suffix")]
        public string RecommendationCollectionSuffix { get; set; } = string.Empty;

        public GenericItemList ApplyAction { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Apply Collection Naming",
                SecondaryText = "Updates existing plugin-owned collections without creating collections for other users.",
                Icon = IconNames.drive_file_rename_outline,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Apply Collection Naming") { CommandId = CollectionSettingsCommands.Apply }
            }
        };
    }

    internal static class CollectionSettingsCommands
    {
        public const string Apply = "collection-settings-apply";
        public const string SaveExpansionSetting = "collection-settings-save-expansion";
        public const string SaveWatchedSettings = "collection-settings-save-watched";
    }
}
