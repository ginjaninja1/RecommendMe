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

        public GenericItemList ExpansionSetting { get; set; } = new GenericItemList();

        public CaptionItem NewUserHeading { get; set; } =
            new CaptionItem("New users: Copy default groups and send policy from");

        public GenericItemList NewUserDefaultsList { get; set; } = new GenericItemList();

        public CaptionItem UserMatrixHeading { get; set; } = new CaptionItem("Per-User Access");

        [DisplayName("Username filter")]
        [Description("Leave blank to show users. At most 10 results are displayed.")]
        [AutoPostBack(AdminCommands.Refresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;

        public LabelItem UserSearchSummary { get; set; } = new LabelItem(string.Empty);

        public GenericItemList UserAccessList { get; set; } = new GenericItemList();
    }
}
