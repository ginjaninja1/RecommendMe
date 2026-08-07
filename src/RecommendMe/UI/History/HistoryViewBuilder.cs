using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.DxGrid;
using MediaBrowser.Controller.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace RecommendMe.UI.History
{
    internal static class HistoryViewBuilder
    {
        public static DxDataGrid BuildEmptyGrid()
        {
            // Mirrors ListManagementUI.Build's working pattern: this
            // constructor - not a manually-populated DxGridOptions - is what
            // actually sets allowColumnReordering, allowColumnResizing,
            // columnResizingMode, columnAutoWidth, filterSyncEnabled and
            // headerFilter. Columns are derived by reflecting over HistoryRow
            // in property-declaration order (DxColumnBuilder.CreateColumns),
            // which is why HistoryRow's property order IS the column order -
            // see the ordering comment on HistoryRow itself.
            //
            // Args: (editObject, keyExpr, multiSelect, disableColumnChooser, showFilterRow, showHeaderFilter)
            var options = new DxGridOptions(new HistoryRow(), nameof(HistoryRow.RecommendationId), false, true, true, true)
            {
                heightMode = DxGridOptions.GridHeightMode.large
            };

            return new DxDataGrid(options);
        }

        /// <summary>
        /// Loads every recommendation record visible to <paramref name="viewer"/>
        /// (privacy isolation + viewer-scope isolation - the only
        /// server-side filtering left; see remarks below) and projects them
        /// into grid rows. Date/sender/recipient/media-type narrowing is left
        /// entirely to DxDataGrid's own filter row on the client.
        /// </summary>
        public static async Task<HistoryRow[]> BuildRowsAsync(User viewer)
        {
            var plugin = Plugin.Instance;
            var all = await plugin.RecommendationStore.GetAllAsync().ConfigureAwait(false);
            await plugin.PermissionService.EnsureUserAccessEntryAsync(viewer).ConfigureAwait(false);
            var adminSettings = await plugin.AdminSettingsStore.GetAsync().ConfigureAwait(false);
            var viewerEntry = adminSettings.UserAccess.First(u => u.UserId == viewer.InternalId);

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

                // Visibility isolation: a viewer always retains access to records
                // they sent or received. For any other non-private record, their
                // current admin send scope must include both participants. This
                // deliberately uses only the admin matrix: recipient opt-outs are
                // about accepting recommendations, not history visibility.
                if (!isSender && !isRecipient)
                {
                    var senderEntry = adminSettings.UserAccess.FirstOrDefault(u => u.UserId == r.SentByUserId);
                    var recipientEntry = adminSettings.UserAccess.FirstOrDefault(u => u.UserId == r.SentToUserId);
                    var bothPartiesAreVisible = !viewerEntry.AccessSuspended
                        && senderEntry != null
                        && !senderEntry.AccessSuspended
                        && recipientEntry != null
                        && !recipientEntry.AccessSuspended
                        && Services.PermissionService.IsTargetAllowed(viewerEntry, r.SentByUserId, adminSettings)
                        && Services.PermissionService.IsTargetAllowed(viewerEntry, r.SentToUserId, adminSettings);

                    if (!bothPartiesAreVisible)
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
                    RecommendationId = r.RecommendationId.ToString("N"),
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
