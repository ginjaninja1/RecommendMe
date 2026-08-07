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

        public CaptionItem GroupHeading { get; set; } = new CaptionItem("Find or create a group");
        [DisplayName("Group search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(GroupSearch))]
        public string GroupSearch { get; set; } = string.Empty;
        public GenericItemList GroupResults { get; set; } = new GenericItemList();

        [DisplayName("New group name")]
        public string NewGroupName { get; set; } = string.Empty;
        public GenericItemList CreateAction { get; set; } = new GenericItemList
        {
            new GenericListItem { PrimaryText = "Create a new group", Icon = IconNames.group_add, Button1 = new ButtonItem("Create") { CommandId = GroupsCommands.Create } }
        };

        [Browsable(false)]
        public string SelectedGroupId { get; set; }
        public CaptionItem SelectedHeading { get; set; } = new CaptionItem("Selected group");
        [DisplayName("Current members")]
        public string CurrentMembers { get; set; } = "Select a group.";
        [DisplayName("Rename group")]
        public string RenameName { get; set; } = string.Empty;
        public GenericItemList GroupActions { get; set; } = new GenericItemList();

        [DisplayName("User search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(UserSearch))]
        public string UserSearch { get; set; } = string.Empty;
        public GenericItemList UserResults { get; set; } = new GenericItemList();

        public CaptionItem UserLookupHeading { get; set; } = new CaptionItem("Find a user’s groups");
        [DisplayName("User membership search")]
        [AutoPostBack(GroupsCommands.Refresh, nameof(MembershipUserSearch))]
        public string MembershipUserSearch { get; set; } = string.Empty;
        public GenericItemList MembershipResults { get; set; } = new GenericItemList();
    }
}
