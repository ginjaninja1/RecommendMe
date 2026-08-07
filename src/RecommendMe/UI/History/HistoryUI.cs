using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Model.Attributes;

namespace RecommendMe.UI.History
{
    /// <summary>
    /// One row of the history grid. Property names/order define the grid
    /// columns built in <see cref="HistoryViewBuilder"/>.
    /// </summary>
    public class HistoryRow
    {
        public string MediaType { get; set; }

        public string Name { get; set; }

        public string DateRecommended { get; set; }

        public string RecommendedBy { get; set; }

        public string RecommendedTo { get; set; }

        public string Private { get; set; }
    }

    /// <summary>
    /// View-model for the Recommendation History dialog. Filters
    /// (date range / sender) are simple dropdowns that trigger a server-side
    /// requery via AutoPostBack; the grid itself (search/sort/column-filter)
    /// is handled entirely client-side by the DxDataGrid over whatever rows
    /// are currently loaded into DataSource.
    /// </summary>
    public class HistoryUI : EditableOptionsBase
    {
        public override string EditorTitle => "Recommendation History";

        public override string EditorDescription =>
            "Showing recommendations you can see, based on what the admin has made visible to you.";

        [DisplayName("Time range")]
        [SelectItemsSource(nameof(DateRangeChoices))]
        [AutoPostBack(HistoryCommands.Refresh, nameof(SelectedDateRange))]
        public string SelectedDateRange { get; set; } = HistoryFilters.Last3Months;

        // NOTE: must be List<EditorSelectOption>, not List<string> - see the
        // matching note on RecommendUI.TargetUserChoices for why a bare
        // string list renders as unselectable "undefined" entries.
        [Browsable(false)]
        public List<EditorSelectOption> DateRangeChoices { get; set; } = ToOptions(HistoryFilters.AllDateRanges);

        [DisplayName("Recommended to")]
        [SelectItemsSource(nameof(RecipientChoices))]
        [AutoPostBack(HistoryCommands.Refresh, nameof(SelectedRecipient))]
        public string SelectedRecipient { get; set; } = HistoryFilters.CurrentUser;

        [Browsable(false)]
        public List<EditorSelectOption> RecipientChoices { get; set; } = ToOptions(HistoryFilters.AllRecipientFilters);

        [DisplayName("From")]
        [SelectItemsSource(nameof(SenderChoices))]
        [AutoPostBack(HistoryCommands.Refresh, nameof(SelectedSender))]
        public string SelectedSender { get; set; } = HistoryFilters.Anyone;

        [Browsable(false)]
        public List<EditorSelectOption> SenderChoices { get; set; } = ToOptions(new[] { HistoryFilters.Anyone });

        /// <summary>The actual grid. Columns are static; DataSource is rebuilt on every refresh.</summary>
        public DxDataGrid Grid { get; set; }

        /// <summary>Builds Value==Name option pairs for filter choices where the raw filter constant is also the label.</summary>
        internal static List<EditorSelectOption> ToOptions(IEnumerable<string> values) =>
            values.Select(v => new EditorSelectOption(v, v)).ToList();
    }
}