using System.Collections.Generic;
using System.ComponentModel;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;

namespace RecommendMe.UI.Recommend
{
    /// <summary>
    /// View-model for the main "Search &amp; Recommend" page. Search results
    /// and the target-user list are rebuilt server-side on every postback;
    /// nothing here is persisted directly (see RecommendPageView for what
    /// actually gets read back off SearchTerm / SelectedTargetUserName /
    /// IsPrivate on submit).
    /// </summary>
    public class RecommendUI : EditableOptionsBase
    {
        public override string EditorTitle => "Recommend";

        public override string EditorDescription => "Search your library and recommend something to another user.";

        public CaptionItem SearchHeading { get; set; } = new CaptionItem("Find something to recommend");

        [DisplayName("Search")]
        [Description("Type a title and press Search.")]
        [AutoPostBack(RecommendCommands.Search, nameof(SearchTerm))]
        public string SearchTerm { get; set; } = string.Empty;

        [DisplayName("Recommend to")]
        [SelectItemsSource(nameof(TargetUserChoices))]
        [AutoPostBack(RecommendCommands.UpdateFormState, nameof(SelectedTargetUserName))]
        public string SelectedTargetUserName { get; set; }

        // NOTE: must be List<EditorSelectOption> (Value/Name pairs), not
        // List<string>. The GenericEdit client renders each entry's .Value
        // and .Name; a bare string has neither, which is why this rendered
        // as N unselectable "undefined" rows for as many users as existed.
        [Browsable(false)]
        public List<EditorSelectOption> TargetUserChoices { get; set; } = new List<EditorSelectOption>();

        [DisplayName("Private recommendation")]
        [Description("Only you and the recipient will see this in the recommendation history.")]
        [AutoPostBack(RecommendCommands.UpdateFormState, nameof(IsPrivate))]
        public bool IsPrivate { get; set; }

        public CaptionItem ResultsHeading { get; set; } = new CaptionItem("Search Results");

        /// <summary>
        /// Each result renders as a GenericListItem with a "Recommend" button
        /// whose CommandId encodes the item's internal id
        /// (RecommendCommands.BuildSendCommandId) - see RecommendCommands.
        /// </summary>
        public GenericItemList SearchResults { get; set; } = new GenericItemList();

        public GenericItemList StatusMessage { get; set; } = new GenericItemList();

        public GenericItemList HistoryLink { get; set; } = new GenericItemList
        {
            new GenericListItem
            {
                PrimaryText = "View Recommendation History",
                Icon = IconNames.link,
                Status = ItemStatus.Succeeded,
                Button1 = new ButtonItem("Open") { CommandId = RecommendCommands.OpenHistory }
            }
        };
    }
}