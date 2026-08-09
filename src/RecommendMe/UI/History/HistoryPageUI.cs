using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;

namespace RecommendMe.UI.History
{
    /// <summary>Landing content for the user-facing History tab.</summary>
    public class HistoryPageUI : EditableOptionsBase
    {
        /// <summary>
        /// Identifies which user this state belongs to. See RecommendUI.OwnerUserId for the
        /// full rationale - same pattern, same reason (IPluginUIView.User is not refreshed on
        /// postbacks and this page's controller instance is shared across every user).
        /// </summary>
        [Browsable(false)]
        public string OwnerUserId { get; set; } = string.Empty;

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