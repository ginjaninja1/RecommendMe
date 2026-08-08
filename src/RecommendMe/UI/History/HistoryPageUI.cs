using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI.History
{
    /// <summary>Landing content for the user-facing History tab.</summary>
    public class HistoryPageUI : EditableOptionsBase
    {
        public override string EditorTitle => "History";

        public override string EditorDescription =>
            "View the recommendation history available to you.";

        public GenericItemList HistoryLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "View Recommendation History",
                Icon = IconNames.history,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Open") { CommandId = HistoryCommands.Open }
            }
        };
    }
}
