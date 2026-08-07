using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;

namespace RecommendMe.UI.Admin
{
    public class GroupsUI : EditableOptionsBase
    {
        public override string EditorTitle => "Groups";
        public override string EditorDescription => "Create groups and manage membership without displaying the full server user list.";

        public CaptionItem CreateHeading { get; set; } = new CaptionItem("Create New Group");
        [DisplayName("Group name")]
        public string NewGroupName { get; set; } = string.Empty;
        public GenericItemList CreateAction { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "Create",
                SecondaryText = string.Empty,
                Icon = IconNames.group_add,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Create") { CommandId = GroupsCommands.Create }
            }
        };

        public CaptionItem AvailableHeading { get; set; } = new CaptionItem("Available Groups");
        [DisplayName("Group search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(GroupSearch))]
        public virtual string GroupSearch { get; set; } = string.Empty;
        public LabelItem GroupSearchSummary { get; set; } = new LabelItem(string.Empty);
        public GenericItemList GroupResults { get; set; } = new GenericItemList();

        public CaptionItem UserLookupHeading { get; set; } = new CaptionItem("Find a user’s groups");
        [DisplayName("User search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(MembershipUserSearch))]
        public virtual string MembershipUserSearch { get; set; } = string.Empty;
        public LabelItem MembershipSearchSummary { get; set; } = new LabelItem(string.Empty);
        public GenericItemList MembershipResults { get; set; } = new GenericItemList();
    }

    public class ExpandedGroupsUI : GroupsUI
    {
        [Browsable(false)]
        public override string GroupSearch { get; set; } = string.Empty;

        [Browsable(false)]
        public override string MembershipUserSearch { get; set; } = string.Empty;
    }
}
