using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Model.Attributes;
using System;
using System.ComponentModel;

namespace RecommendMe.UI.History
{
    /// <summary>
    /// One row of the history grid. Property DECLARATION ORDER is the grid's
    /// default column order (DxColumnBuilder.CreateColumns reflects over the
    /// type via TypeDescriptor.GetProperties) - see HistoryViewBuilder.BuildEmptyGrid.
    /// </summary>
    public class HistoryRow
    {
        /// <summary>Grid row key (DxGridOptions keyExpr). Hidden - not a user-facing column.</summary>
        [Browsable(false)]
        public string RecommendationId { get; set; }

        [DisplayName("Recommended To")]
        public string RecommendedTo { get; set; }

        [DisplayName("Recommended By")]
        public string RecommendedBy { get; set; }

        [DisplayName("Private")]
        public string Private { get; set; }

        [DisplayName("Date Recommended")]
        public string DateRecommended { get; set; }

        [DisplayName("Media Type")]
        public string MediaType { get; set; }

        [DisplayName("Name")]
        public string Name { get; set; }
    }

    /// <summary>
    /// View-model for the Recommendation History dialog. All rows the viewer
    /// is allowed to see (per privacy/visibility isolation - a security
    /// boundary, so it stays server-side) are loaded once when the dialog
    /// opens. Date range / sender / recipient / media-type filtering is
    /// handled entirely client-side by DxDataGrid's own filter row and
    /// header filters over that row set (see HistoryViewBuilder.BuildEmptyGrid) -
    /// there is no server postback filter here to go stale or silently drop
    /// rows.
    /// </summary>
    public class HistoryUI : EditableOptionsBase
    {
        public override string EditorTitle => "Recommendation History";

        public override string EditorDescription =>
            "Recommendations you can see, based on what the admin has made visible to you. Use the column headers to filter or search.";

        /// <summary>
        /// The grid. Columns/options are static (HistoryViewBuilder.BuildEmptyGrid);
        /// row data is NOT read from Grid.Options.dataSource by the editor
        /// host - it's read from whichever property [GridDataSource] points
        /// at, i.e. Rows below. Confirmed against the decompiled
        /// EditorDxGrid/GridDataSourceAttribute and the working pattern in
        /// ListManagementUI.PlaylistGrid.
        /// </summary>
        [GridDataSource(nameof(Rows))]
        public DxDataGrid Grid { get; set; }

        [Browsable(false)]
        public HistoryRow[] Rows { get; set; } = Array.Empty<HistoryRow>();
    }
}