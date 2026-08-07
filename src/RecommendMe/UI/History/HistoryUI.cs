using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Model.Attributes;
using System;
using System.ComponentModel;

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
    /// View-model for the Recommendation History dialog. All rows the viewer
    /// is allowed to see (per privacy/visibility isolation - a security
    /// boundary, so it stays server-side) are loaded once when the dialog
    /// opens. Date range / sender / recipient / media-type filtering is
    /// handled entirely client-side by DxDataGrid's native filter row over
    /// that row set - there is no server postback filter here to go stale or
    /// silently drop rows. See HistoryViewBuilder.BuildRowsAsync.
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