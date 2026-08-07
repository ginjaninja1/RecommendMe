using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Controller.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RecommendMe.UI.History
{
    internal static class HistoryViewBuilder
    {
        public static DxDataGrid BuildEmptyGrid()
        {
            var columns = new DxGridColumnList
            {
                new DxGridColumn { dataField = nameof(HistoryRow.MediaType), caption = "Media Type", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string },
                new DxGridColumn { dataField = nameof(HistoryRow.Name), caption = "Name", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string },
                new DxGridColumn { dataField = nameof(HistoryRow.DateRecommended), caption = "Date Recommended", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string },
                new DxGridColumn { dataField = nameof(HistoryRow.RecommendedBy), caption = "Recommended By", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string },
                new DxGridColumn { dataField = nameof(HistoryRow.RecommendedTo), caption = "Recommended To", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string },
                new DxGridColumn { dataField = nameof(HistoryRow.Private), caption = "Private", allowSorting = true, allowFiltering = true, dataType = DxGridColumn.ColumnDataType.@string }
            };

            var options = new DxGridOptions
            {
                columns = columns,
                dataSource = Array.Empty<object>(),
                filterRow = new DxGridFilterRow(),
                sorting = new DxGridSorting { mode = DxGridSorting.GridSortingMode.multiple },
                paging = new DxGridPaging { enabled = true, pageSize = 25 },
                columnAutoWidth = true,
                showBorders = true
            };

            return new DxDataGrid(options);
        }

        /// <summary>
        /// Loads every recommendation record visible to <paramref name="viewer"/>
        /// (privacy isolation + sender-visibility isolation - the only
        /// server-side filtering left; see remarks below) and projects them
        /// into grid rows. Date/sender/recipient/media-type narrowing is left
        /// entirely to DxDataGrid's own filter row on the client.
        /// </summary>
        public static async Task<HistoryRow[]> BuildRowsAsync(User viewer)
        {
            var plugin = Plugin.Instance;
            var all = await plugin.RecommendationStore.GetAllAsync().ConfigureAwait(false);
            var adminSettings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);

            plugin.Logger.Debug(
                "RecommendMe: History - viewer={0} ({1}), total records={2}",
                viewer.Name, viewer.InternalId, all.Count);

            var visible = all.Where(r =>
            {
                var isSender = r.SentByUserId == viewer.InternalId;
                var isRecipient = r.SentToUserId == viewer.InternalId;

                // Privacy isolation: a private record is only visible to its sender/recipient.
                if (r.IsPrivate && !isSender && !isRecipient)
                {
                    return false;
                }

                // Visibility isolation: for non-private records the viewer must be
                // the sender/recipient, or the OTHER party's send permission must
                // currently cover that recipient (approximated here via the same
                // SendMode check used for real permission enforcement).
                if (!isSender && !isRecipient)
                {
                    var senderEntry = adminSettings.UserAccess.FirstOrDefault(u => u.UserId == r.SentByUserId);
                    var otherPartyIsVisible = senderEntry != null
                        && !senderEntry.AccessSuspended
                        && Services.PermissionService.IsTargetAllowed(senderEntry, r.SentToUserId);

                    if (!otherPartyIsVisible)
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();

            plugin.Logger.Debug(
                "RecommendMe: History - viewer={0}, records visible after privacy/visibility isolation={1}",
                viewer.Name, visible.Count);

            return visible
                .OrderByDescending(r => r.DateSentUtc)
                .Select(r => new HistoryRow
                {
                    MediaType = r.MediaType,
                    Name = r.ItemName,
                    DateRecommended = r.DateSentUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    RecommendedBy = r.SentByUserName,
                    RecommendedTo = r.SentToUserName,
                    Private = r.IsPrivate ? "Y" : "N"
                })
                .ToArray();
        }
    }
}