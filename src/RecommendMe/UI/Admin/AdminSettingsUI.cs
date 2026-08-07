using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;

namespace RecommendMe.UI.Admin
{
    public class AdminSettingsUI : EditableOptionsBase
    {
        public override string EditorTitle => "Users";
        public override string EditorDescription => "Control deterministic send and receive access for every user.";

        [DisplayName("Always Expand Users and Groups")]
        [Description("On: show every user and group and allow the page to scroll. Off: show search filters and limit each result list to 10 items.")]
        [AutoPostBack(AdminCommands.ToggleExpansion, nameof(AlwaysExpandUsersAndGroups))]
        public bool AlwaysExpandUsersAndGroups { get; set; } = true;

        public CaptionItem NewUserHeading { get; set; } =
            new CaptionItem("New users: Copy default groups and send policy from");

        public GenericItemList NewUserDefaultsList { get; set; } = new GenericItemList();

        public CaptionItem UserMatrixHeading { get; set; } = new CaptionItem("Per-User Access");

        [DisplayName("Username filter")]
        [Description("Leave blank to show users. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.Refresh, nameof(UserSearch))]
        [VisibleCondition(nameof(AlwaysExpandUsersAndGroups), SimpleCondition.IsFalse)]
        public string UserSearch { get; set; } = string.Empty;

        [VisibleCondition(nameof(AlwaysExpandUsersAndGroups), SimpleCondition.IsFalse)]
        public LabelItem UserSearchSummary { get; set; } = new LabelItem(string.Empty);

        public GenericItemList UserAccessList { get; set; } = new GenericItemList();
    }
}
