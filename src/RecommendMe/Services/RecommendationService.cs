using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
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
        AlreadyWatchedByRecipient,
        AlreadyActiveRecommendation
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
        private readonly IUserDataManager userDataManager;
        private readonly ILogger logger;

        public RecommendationService(
            PermissionService permissionService,
            CollectionSyncService collectionSyncService,
            NotificationService notificationService,
            RecommendationStore recommendationStore,
            IUserDataManager userDataManager,
            ILogger logger)
        {
            this.permissionService = permissionService;
            this.collectionSyncService = collectionSyncService;
            this.notificationService = notificationService;
            this.recommendationStore = recommendationStore;
            this.userDataManager = userDataManager;
            this.logger = logger;
        }

        /// <summary>
        /// Sends <paramref name="item"/> from <paramref name="sender"/> to
        /// <paramref name="recipient"/>: checks permission, checks the two
        /// pre-conditions (not already watched, not already actively
        /// recommended to them), then persists the record, adds the item to
        /// the recipient's collection, and fires a session toast.
        /// </summary>
        public async Task<RecommendationResult> SendRecommendationAsync(
            User sender,
            User recipient,
            BaseItem item,
            string mediaType,
            bool isPrivate)
        {
            var permitted = await this.permissionService.CanSendAsync(sender, recipient, mediaType).ConfigureAwait(false);
            if (!permitted)
            {
                return RecommendationResult.NotPermitted;
            }

            var recipientData = this.userDataManager.GetUserData(recipient, item);
            if (recipientData != null && recipientData.Played)
            {
                return RecommendationResult.AlreadyWatchedByRecipient;
            }

            var alreadyActive = await this.recommendationStore
                .HasActiveRecommendationAsync(recipient.InternalId, item.InternalId)
                .ConfigureAwait(false);
            if (alreadyActive)
            {
                return RecommendationResult.AlreadyActiveRecommendation;
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

            return RecommendationResult.Success;
        }

        /// <summary>
        /// Called from the IUserDataManager.UserDataSaved hook in Plugin.cs
        /// whenever a user's play state changes. If the item just became
        /// "Played" and it's one of this user's active recommendations,
        /// resolve the record(s) and pull the item out of their collection.
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
                "RecommendMe: auto-removed item {0} from {1}'s recommendation collection after it was watched.",
                itemId,
                user.Name);
        }
    }
}
