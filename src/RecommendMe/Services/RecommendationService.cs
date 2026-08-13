using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using System;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Top-level orchestrator for sending a recommendation and for reacting
    /// to a recipient watching a recommended item. This is the only class
    /// that touches all three of: permission rules, the JSON recommendation
    /// log, and the Emby collection - everything else in Services/ is a
    /// single-purpose collaborator this class composes.
    /// </summary>
    internal class RecommendationService
    {
        private readonly PermissionService permissionService;
        private readonly CollectionSyncService collectionSyncService;
        private readonly NotificationService notificationService;
        private readonly RecommendationStore recommendationStore;
        private readonly CollectionRegistryStore collectionRegistryStore;
        private readonly AdminSettingsStore adminSettingsStore;
        private readonly IUserDataManager userDataManager;
        private readonly IUserManager userManager;
        private readonly ILibraryManager libraryManager;
        private readonly ILogger logger;
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);

        public RecommendationService(
            PermissionService permissionService,
            CollectionSyncService collectionSyncService,
            NotificationService notificationService,
            RecommendationStore recommendationStore,
            CollectionRegistryStore collectionRegistryStore,
            AdminSettingsStore adminSettingsStore,
            IUserDataManager userDataManager,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ILogger logger)
        {
            this.permissionService = permissionService;
            this.collectionSyncService = collectionSyncService;
            this.notificationService = notificationService;
            this.recommendationStore = recommendationStore;
            this.collectionRegistryStore = collectionRegistryStore;
            this.adminSettingsStore = adminSettingsStore;
            this.userDataManager = userDataManager;
            this.userManager = userManager;
            this.libraryManager = libraryManager;
            this.logger = logger;
        }

        /// <summary>
        /// Sends <paramref name="item"/> from <paramref name="sender"/> to
        /// <paramref name="recipient"/>.
        ///
        /// Submission-time gate: all of the following
        /// must hold, and NONE of them consult the JSON recommendation log -
        /// that log is write-only history for the History dialog, never a
        /// gating source. Gating against it was the root cause of recipients
        /// getting permanently stuck with "already has an active
        /// recommendation" after manually removing an item from their
        /// collection outside the watched-cleanup flow, since nothing ever
        /// resolved the log's Active status in that case:
        ///   1. Recipient's own preferences currently accept recommendations
        ///      from this sender, for this media type (PermissionService).
        ///   2. The item is visible to the recipient per Emby's own access
        ///      rules (parental rating, blocked tags, folder/library access) -
        ///      via BaseItem.IsVisible(User), never reimplemented manually.
        ///   3. The item is not currently in the recipient's recommendation
        ///      collection, checked live (CollectionSyncService).
        ///   4. When enabled by the admin, the item is not already marked
        ///      watched for the recipient.
        /// </summary>
        public async Task<RecommendationSendResult> SendRecommendationAsync(
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

            await this.sendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var permission = await this.permissionService.CanSendAsync(sender, recipient, mediaType).ConfigureAwait(false);
                this.logger.Debug("Permission check result = {0}", permission);
                switch (permission)
                {
                    case SendPermissionResult.AdminBlocked:
                        return RecommendationSendResult.NotPermitted;
                    case SendPermissionResult.RecipientBlockedSender:
                        return RecommendationSendResult.RecipientBlockedSender;
                    case SendPermissionResult.RecipientOptedOutMediaType:
                        return RecommendationSendResult.RecipientOptedOutMediaType;
                }

                if (!item.IsVisible(recipient))
                {
                    this.logger.Debug(
                        "Blocked - item {0} is not visible to recipient {1} (parental rating, blocked tags, or folder access).",
                        item.InternalId,
                        recipient.Name);
                    return RecommendationSendResult.RecipientCannotAccessItem;
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
                        return RecommendationSendResult.AlreadyWatchedByRecipient;
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
                    return RecommendationSendResult.AlreadyInRecipientCollection;
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

                try
                {
                    await this.collectionSyncService.AddItemAsync(recipient, item).ConfigureAwait(false);
                    await this.recommendationStore.AddAsync(record).ConfigureAwait(false);
                }
                catch
                {
                    try
                    {
                        await this.collectionSyncService.RemoveItemAsync(recipient, item.InternalId).ConfigureAwait(false);
                    }
                    catch (Exception compensationError)
                    {
                        this.logger.ErrorException(
                            "Failed to roll back collection membership for item {0} and user {1}",
                            compensationError,
                            item.InternalId,
                            recipient.InternalId);
                    }

                    throw;
                }

                this.logger.Debug(
                    "Recommendation {0} recorded and item {1} added to {2}'s collection.",
                    record.RecommendationId,
                    item.InternalId,
                    recipient.Name);
            }
            finally
            {
                this.sendGate.Release();
            }

            await this.notificationService
                .NotifyRecommendationReceivedAsync(recipient, sender, item.Name, mediaType)
                .ConfigureAwait(false);

            return RecommendationSendResult.Success;
        }

        /// <summary>
        /// Called from the IUserDataManager.UserDataSaved hook in Plugin.cs whenever a user's
        /// play state becomes Played. Live collection membership decides whether removal is
        /// required. Recommendation history is an event log and is not changed by removal.
        /// </summary>
        public async Task HandleItemWatchedAsync(long itemId, User user)
        {
            var isMember = await this.collectionSyncService
                .IsItemInRecipientCollectionAsync(user, itemId)
                .ConfigureAwait(false);
            if (!isMember)
            {
                this.logger.Debug(
                    "Watched item {0} is not in {1}'s registered recommendation collection; no cleanup required.",
                    itemId,
                    user.Name);
                return;
            }

            if (!await this.collectionSyncService.RemoveItemAsync(user, itemId).ConfigureAwait(false))
            {
                this.logger.Warn(
                    "Could not remove watched item {0} from {1}'s registered recommendation collection.",
                    itemId,
                    user.Name);
                return;
            }

            this.logger.Info(
                "Auto-removed watched item {0} from {1}'s recommendation collection.",
                itemId,
                user.Name);
        }

        /// <summary>
        /// Reconciles every live member of every registered recommendation collection against
        /// its owner's current Emby watched state. Recommendation history is neither read nor
        /// changed: it is an event log, not collection state.
        /// </summary>
        public async Task<int> ClearWatchedRecommendationsAsync(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var entries = await this.collectionRegistryStore.GetAllAsync().ConfigureAwait(false);
            var users = this.userManager.GetUserList(new UserQuery()).ToDictionary(user => user.InternalId);
            var removed = 0;
            var assessed = 0;

            this.logger.Info(
                "Watched-recommendation task found {0} registered recommendation collection(s) and {1} Emby user(s).",
                entries.Count,
                users.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(entries.Count == 0 ? 100 : (double)i / entries.Count * 100);

                var entry = entries[i];
                if (!users.TryGetValue(entry.UserId, out var user))
                {
                    this.logger.Warn(
                        "Watched-recommendation task skipped registry entry for missing user {0}; collection={1}, publicId={2}, storedUserName='{3}', storedCollectionName='{4}'.",
                        entry.UserId,
                        entry.CollectionId,
                        entry.EmbyCollectionId,
                        entry.UserName,
                        entry.CollectionName);
                    continue;
                }

                var collection = this.libraryManager.GetItemById(entry.CollectionId) as BoxSet;
                if (collection == null)
                {
                    this.logger.Warn(
                        "Watched-recommendation task skipped {0} ({1}): collection {2} does not resolve to a BoxSet.",
                        user.Name,
                        user.InternalId,
                        entry.CollectionId);
                    continue;
                }

                var publicId = collection.Id.ToString("N");
                if (!string.Equals(entry.EmbyCollectionId, publicId, StringComparison.OrdinalIgnoreCase))
                {
                    this.logger.Warn(
                        "Watched-recommendation task skipped {0} ({1}): collection {2} public identity mismatch; registry={3}, live={4}.",
                        user.Name,
                        user.InternalId,
                        entry.CollectionId,
                        entry.EmbyCollectionId,
                        publicId);
                    continue;
                }

                await this.collectionRegistryStore.RegisterAsync(
                    user.InternalId,
                    user.Name,
                    collection.InternalId,
                    collection.Name,
                    publicId).ConfigureAwait(false);

                var itemIds = this.libraryManager.GetInternalItemIds(new InternalItemsQuery
                {
                    CollectionIds = new[] { collection.InternalId }
                });

                this.logger.Info(
                    "Watched-recommendation task assessing collection '{0}' ({1}, publicId={2}) for user {3} ({4}): {5} member(s).",
                    collection.Name,
                    collection.InternalId,
                    publicId,
                    user.Name,
                    user.InternalId,
                    itemIds.Length);

                foreach (var itemId in itemIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    assessed++;

                    var item = this.libraryManager.GetItemById(itemId);
                    if (item == null)
                    {
                        this.logger.Debug(
                            "Watched-recommendation assessment: collection='{0}', user={1} ({2}), item={3}, result=skipped-item-not-found.",
                            collection.Name,
                            user.Name,
                            user.InternalId,
                            itemId);
                        continue;
                    }

                    var userData = this.userDataManager.GetUserData(user, item);
                    var watched = userData != null && userData.Played;

                    this.logger.Debug(
                        "Watched-recommendation assessment: collection='{0}' ({1}), user={2} ({3}), item='{4}' ({5}), watched={6}, lastPlayed={7}, resumeTicks={8}.",
                        collection.Name,
                        collection.InternalId,
                        user.Name,
                        user.InternalId,
                        item.Name,
                        item.InternalId,
                        watched,
                        userData?.LastPlayedDate,
                        userData?.PlaybackPositionTicks ?? 0);

                    if (!watched)
                    {
                        continue;
                    }

                    if (!await this.collectionSyncService.RemoveItemAsync(user, item.InternalId).ConfigureAwait(false))
                    {
                        this.logger.Warn(
                            "Watched-recommendation task failed to remove watched item '{0}' ({1}) from '{2}' for {3}.",
                            item.Name,
                            item.InternalId,
                            collection.Name,
                            user.Name);
                        continue;
                    }

                    removed++;
                    this.logger.Info(
                        "Watched-recommendation task removed watched item '{0}' ({1}) from '{2}' for {3}.",
                        item.Name,
                        item.InternalId,
                        collection.Name,
                        user.Name);
                }
            }

            progress?.Report(100);
            this.logger.Info(
                "Watched-recommendation task assessed {0} live collection member(s) and removed {1} watched item(s).",
                assessed,
                removed);
            return removed;
        }
    }
}
