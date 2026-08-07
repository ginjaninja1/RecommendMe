using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using System;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    public enum RecommendationResult
    {
        Success,
        NotPermitted,
        RecipientBlockedSender,
        RecipientOptedOutMediaType,
        AlreadyWatchedByRecipient,

        /// <summary>
        /// The item is currently a member of the recipient's recommendation
        /// collection (checked live - see
        /// CollectionSyncService.IsItemInRecipientCollectionAsync). Renamed
        /// from the old AlreadyActiveRecommendation: that name described a
        /// check against the JSON history log, which is no longer part of
        /// this gate at all (see remarks on SendRecommendationAsync).
        /// </summary>
        AlreadyInRecipientCollection
    }

    /// <summary>
    /// Top-level orchestrator for sending a recommendation and for reacting
    /// to a recipient watching a recommended item. This is the only class
    /// that touches all three of: permission rules, the JSON recommendation
    /// log, and the Emby collection - everything else in Services/ is a
    /// single-purpose collaborator this class composes.
    /// </summary>
    public class RecommendationService
    {
        private readonly PermissionService permissionService;
        private readonly CollectionSyncService collectionSyncService;
        private readonly NotificationService notificationService;
        private readonly RecommendationStore recommendationStore;
        private readonly AdminSettingsStore adminSettingsStore;
        private readonly IUserDataManager userDataManager;
        private readonly ILogger logger;

        public RecommendationService(
            PermissionService permissionService,
            CollectionSyncService collectionSyncService,
            NotificationService notificationService,
            RecommendationStore recommendationStore,
            AdminSettingsStore adminSettingsStore,
            IUserDataManager userDataManager,
            ILogger logger)
        {
            this.permissionService = permissionService;
            this.collectionSyncService = collectionSyncService;
            this.notificationService = notificationService;
            this.recommendationStore = recommendationStore;
            this.adminSettingsStore = adminSettingsStore;
            this.userDataManager = userDataManager;
            this.logger = logger;
        }

        /// <summary>
        /// Sends <paramref name="item"/> from <paramref name="sender"/> to
        /// <paramref name="recipient"/>.
        ///
        /// Submission-time gate (spec, 2026-08-07): all of the following
        /// must hold, and NONE of them consult the JSON recommendation log -
        /// that log is write-only history for the History dialog, never a
        /// gating source. Gating against it was the root cause of recipients
        /// getting permanently stuck with "already has an active
        /// recommendation" after manually removing an item from their
        /// collection outside the watched-cleanup flow, since nothing ever
        /// resolved the log's Active status in that case:
        ///   1. Recipient's own preferences currently accept recommendations
        ///      from this sender, for this media type (PermissionService).
        ///   2. The item is not currently in the recipient's recommendation
        ///      collection, checked live (CollectionSyncService).
        ///   3. When enabled by the admin, the item is not already marked
        ///      watched for the recipient.
        /// </summary>
        public async Task<RecommendationResult> SendRecommendationAsync(
            User sender,
            User recipient,
            BaseItem item,
            string mediaType,
            bool isPrivate)
        {
            this.logger.Debug(
                "SendRecommendationAsync - sender={0} ({1}), recipient={2} ({3}), item={4} '{5}' ({6}), private={7}",
                sender.Name, sender.InternalId,
                recipient.Name, recipient.InternalId,
                item.InternalId, item.Name, mediaType,
                isPrivate);

            var permission = await this.permissionService.CanSendAsync(sender, recipient, mediaType).ConfigureAwait(false);
            this.logger.Debug("Permission check result = {0}", permission);
            switch (permission)
            {
                case SendPermissionResult.AdminBlocked:
                    return RecommendationResult.NotPermitted;
                case SendPermissionResult.RecipientBlockedSender:
                    return RecommendationResult.RecipientBlockedSender;
                case SendPermissionResult.RecipientOptedOutMediaType:
                    return RecommendationResult.RecipientOptedOutMediaType;
            }

            var settings = await this.adminSettingsStore.GetAsync().ConfigureAwait(false);
            if (settings.PreventWatchedRecommendations)
            {
                var recipientData = this.userDataManager.GetUserData(recipient, item);
                if (recipientData != null && recipientData.Played)
                {
                    this.logger.Debug(
                        "Blocked - item {0} already watched by {1}.",
                        item.InternalId,
                        recipient.Name);
                    return RecommendationResult.AlreadyWatchedByRecipient;
                }
            }

            var alreadyInCollection = await this.collectionSyncService
                .IsItemInRecipientCollectionAsync(recipient, item.InternalId)
                .ConfigureAwait(false);
            if (alreadyInCollection)
            {
                this.logger.Debug(
                    "Blocked - item {0} already in {1}'s recommendation collection.",
                    item.InternalId,
                    recipient.Name);
                return RecommendationResult.AlreadyInRecipientCollection;
            }

            var record = new RecommendationRecord
            {
                ItemId = item.InternalId,
                ItemName = item.Name,
                MediaType = mediaType,
                SentByUserId = sender.InternalId,
                SentByUserName = sender.Name,
                SentToUserId = recipient.InternalId,
                SentToUserName = recipient.Name,
                IsPrivate = isPrivate
            };

            await this.recommendationStore.AddAsync(record).ConfigureAwait(false);
            await this.collectionSyncService.AddItemAsync(recipient, item).ConfigureAwait(false);

            this.notificationService.NotifyRecommendationReceived(recipient, sender, item.Name, mediaType);

            this.logger.Debug(
                "Recommendation {0} recorded and item {1} added to {2}'s collection.",
                record.RecommendationId,
                item.InternalId,
                recipient.Name);

            return RecommendationResult.Success;
        }

        /// <summary>
        /// Called from the IUserDataManager.UserDataSaved hook in Plugin.cs
        /// whenever a user's play state changes. If the item just became
        /// "Played" and it's one of this user's active recommendations,
        /// resolve the record(s) and pull the item out of their collection.
        /// This still uses the JSON log (unlike the submission gate above) -
        /// it's the log's one legitimate consumer, since it's the only place
        /// that knows which sender(s) to eventually show in history as
        /// "resolved by watching" rather than just vanishing silently.
        /// </summary>
        public async Task HandleItemWatchedAsync(long userId, long itemId, User user)
        {
            var resolved = await this.recommendationStore.ResolveWatchedAsync(userId, itemId).ConfigureAwait(false);
            if (resolved.Count == 0)
            {
                return;
            }

            await this.collectionSyncService.RemoveItemAsync(user, itemId).ConfigureAwait(false);

            this.logger.Info(
                "Auto-removed item {0} from {1}'s recommendation collection after it was watched.",
                itemId,
                user.Name);
        }

        /// <summary>Reconciles active recommendations against Emby's current watched state.</summary>
        public async Task<int> ClearWatchedRecommendationsAsync(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var active = (await this.recommendationStore.GetAllAsync().ConfigureAwait(false))
                .Where(r => r.Status == RecommendationStatus.Active)
                .GroupBy(r => new { r.SentToUserId, r.ItemId })
                .ToArray();
            var users = Plugin.Instance.GetAllUsers().ToDictionary(u => u.InternalId);
            var removed = 0;

            for (var i = 0; i < active.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(active.Length == 0 ? 100 : (double)i / active.Length * 100);

                var key = active[i].Key;
                if (!users.TryGetValue(key.SentToUserId, out var user)) continue;

                var item = Plugin.Instance.LibraryManager.GetItemById(key.ItemId);
                if (item == null) continue;

                var userData = this.userDataManager.GetUserData(user, item);
                if (userData == null || !userData.Played) continue;

                var resolved = await this.recommendationStore.ResolveWatchedAsync(key.SentToUserId, key.ItemId).ConfigureAwait(false);
                if (resolved.Count == 0) continue;

                await this.collectionSyncService.RemoveItemAsync(user, key.ItemId).ConfigureAwait(false);
                removed++;
            }

            progress?.Report(100);
            return removed;
        }
    }
}
