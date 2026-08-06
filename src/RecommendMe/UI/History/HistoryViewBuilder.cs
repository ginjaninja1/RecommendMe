using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Controller.Entities;
using RecommendMe.Models;

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
        /// Loads, filters (date range / recipient / sender / visibility /
        /// privacy), and projects recommendation records into grid rows for
        /// the given viewing user.
        /// </summary>
        public static async Task<object[]> BuildRowsAsync(User viewer, string dateRangeFilter, string recipientFilter, string senderFilter)
        {
            var plugin = Plugin.Instance;
            var all = await plugin.RecommendationStore.GetAllAsync().ConfigureAwait(false);
            var adminSettings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);

            var cutoff = DateRangeToCutoffUtc(dateRangeFilter);

            var visible = all.Where(r =>
            {
                if (cutoff.HasValue && r.DateSentUtc < cutoff.Value)
                {
                    return false;
                }

                var isSender = r.SentByUserId == viewer.InternalId;
                var isRecipient = r.SentToUserId == viewer.InternalId;

                // Privacy isolation: a private record is only visible to its sender/recipient.
                if (r.IsPrivate && !isSender && !isRecipient)
                {
                    return false;
                }

                // Visibility isolation: for non-private records the viewer must be
                // the sender/recipient, or the OTHER party must be someone the
                // admin has made visible to the viewer (approximated here via the
                // same send/receive scope used for permission checks).
                if (!isSender && !isRecipient)
                {
                    var otherPartyIsVisible =
                        IsInScope(adminSettings.SendScope, adminSettings.SendScopeUserIds, r.SentByUserId) &&
                        IsInScope(adminSettings.ReceiveScope, adminSettings.ReceiveScopeUserIds, r.SentToUserId);

                    if (!otherPartyIsVisible)
                    {
                        return false;
                    }
                }

                if (recipientFilter == HistoryFilters.CurrentUser && !isRecipient)
                {
                    return false;
                }

                if (senderFilter != HistoryFilters.Anyone && !string.Equals(r.SentByUserName, senderFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            });

            return visible
                .OrderByDescending(r => r.DateSentUtc)
                .Select(r => (object)new HistoryRow
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

        private static bool IsInScope(AccessScope scope, List<long> allowList, long userId) =>
            scope == AccessScope.AllUsers || allowList.Contains(userId);

        private static DateTime? DateRangeToCutoffUtc(string dateRangeFilter)
        {
            switch (dateRangeFilter)
            {
                case HistoryFilters.Last1Month: return DateTime.UtcNow.AddMonths(-1);
                case HistoryFilters.Last3Months: return DateTime.UtcNow.AddMonths(-3);
                case HistoryFilters.Last6Months: return DateTime.UtcNow.AddMonths(-6);
                default: return null;
            }
        }
    }
}
