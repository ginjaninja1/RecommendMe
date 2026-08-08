using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using RecommendMe.Models;
using RecommendMe.Storage;

namespace RecommendMe.Services
{
    /// <summary>
    /// Sends recommendation notifications to whichever of the recipient's
    /// sessions are currently online. If none are online, the notification
    /// is queued to disk and delivered from a SessionStarted handler once
    /// the recipient reconnects.
    ///
    /// There is no delivery-receipt concept anywhere in the Emby session
    /// API - SendMessageCommand is fire-and-forget. "Online" here means
    /// "present in ISessionManager.Sessions for this user", which is the
    /// same signal the pre-queueing version of this class used; SessionInfo
    /// also exposes an IsActive property but that reflects whether a
    /// session's ISessionController(s) report themselves active (used for
    /// remote-control support), not whether the user is connected, so it is
    /// deliberately not used here.
    /// </summary>
    internal class NotificationService
    {
        private static readonly TimeSpan DeliveryDelay = TimeSpan.FromSeconds(20);

        private readonly ISessionManager sessionManager;
        private readonly PendingNotificationStore pendingNotificationStore;
        private readonly ILogger logger;
        private readonly object lifecycleLock = new object();
        private readonly ConcurrentDictionary<long, byte> usersWithDeliveryInFlight = new ConcurrentDictionary<long, byte>();
        private CancellationTokenSource shutdownTokenSource;
        private bool isRunning;

        public NotificationService(
            ISessionManager sessionManager,
            PendingNotificationStore pendingNotificationStore,
            ILogger logger)
        {
            this.sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            this.pendingNotificationStore = pendingNotificationStore ?? throw new ArgumentNullException(nameof(pendingNotificationStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Subscribes to session lifecycle events. Call once during plugin startup.</summary>
        public void Start()
        {
            lock (this.lifecycleLock)
            {
                if (this.isRunning)
                {
                    return;
                }

                this.shutdownTokenSource = new CancellationTokenSource();
                this.sessionManager.SessionStarted += this.OnSessionStarted;
                this.isRunning = true;
            }
        }

        /// <summary>
        /// Unsubscribes and cancels any in-progress 20s delivery waits. This
        /// does not block waiting for those waits to unwind - a queued
        /// notification is safe to leave on disk for delivery next time the
        /// recipient's session starts, so shutdown does not need to await it
        /// the way Plugin.Dispose awaits watched-item cleanup.
        /// </summary>
        public void Dispose()
        {
            lock (this.lifecycleLock)
            {
                if (!this.isRunning)
                {
                    return;
                }

                this.sessionManager.SessionStarted -= this.OnSessionStarted;
                this.shutdownTokenSource.Cancel();
                this.shutdownTokenSource.Dispose();
                this.isRunning = false;
            }
        }

        public async Task NotifyRecommendationReceivedAsync(
            User recipient,
            User sender,
            string itemName,
            string mediaType)
        {
            var text = BuildMessageText(sender.Name, itemName, mediaType);
            var recipientSessions = this.GetOnlineSessions(recipient.InternalId);

            if (recipientSessions.Length == 0)
            {
                this.logger.Debug(
                    "Recipient {0} has no active sessions; queuing notification for item '{1}'.",
                    recipient.Name,
                    itemName);

                await this.pendingNotificationStore.AddAsync(new PendingNotificationRecord
                {
                    RecipientUserId = recipient.InternalId,
                    RecipientUserName = recipient.Name,
                    MessageText = text
                }).ConfigureAwait(false);

                return;
            }

            await this.SendToSessionsAsync(recipientSessions, text).ConfigureAwait(false);
        }

        private static string BuildMessageText(string senderName, string itemName, string mediaType)
        {
            return $"{senderName} recommended {mediaType} {itemName}. Check in collections.";
        }

        private SessionInfo[] GetOnlineSessions(long userInternalId)
        {
            return this.sessionManager.Sessions
                .Where(session => session.UserInternalId == userInternalId)
                .ToArray();
        }

        private async Task SendToSessionsAsync(SessionInfo[] sessions, string text)
        {
            var command = new MessageCommand
            {
                Header = "New Recommendation",
                Text = text,
                TimeoutMs = 8000
            };

            var sends = sessions.Select(session => this.SendAsync(session.Id, command));
            await Task.WhenAll(sends).ConfigureAwait(false);
        }

        private async Task SendAsync(string sessionId, MessageCommand command)
        {
            try
            {
                await this.sessionManager.SendMessageCommand(
                    controllingSessionId: null,
                    sessionId: sessionId,
                    command: command,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Unable to send recommendation notification to session {0}", ex, sessionId);
            }
        }

        /// <summary>
        /// Fires for every new session for every user. usersWithDeliveryInFlight
        /// guards against a user reconnecting twice within the 20s delay
        /// window starting two overlapping deliveries that would both see,
        /// and both try to clear, the same queued records.
        /// </summary>
        private void OnSessionStarted(object sender, SessionEventArgs e)
        {
            var userInternalId = e?.SessionInfo?.UserInternalId ?? 0;
            if (userInternalId == 0)
            {
                return;
            }

            if (!this.usersWithDeliveryInFlight.TryAdd(userInternalId, 0))
            {
                return;
            }

            var cancellationToken = this.shutdownTokenSource.Token;
            _ = this.DeliverQueuedNotificationsAsync(userInternalId, cancellationToken)
                .ContinueWith(
                    completedTask => this.usersWithDeliveryInFlight.TryRemove(userInternalId, out _),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        /// <summary>
        /// Waits <see cref="DeliveryDelay"/> then re-checks presence rather
        /// than assuming it, since a session can start and end within that
        /// window. If the recipient is offline at the recheck, the
        /// notification(s) are left queued for the next SessionStarted
        /// event - there is no delivery receipt to justify a retry loop.
        /// </summary>
        private async Task DeliverQueuedNotificationsAsync(long recipientUserId, CancellationToken cancellationToken)
        {
            try
            {
                var pending = await this.pendingNotificationStore.GetForUserAsync(recipientUserId).ConfigureAwait(false);
                if (pending.Count == 0)
                {
                    return;
                }

                await Task.Delay(DeliveryDelay, cancellationToken).ConfigureAwait(false);

                var recipientSessions = this.GetOnlineSessions(recipientUserId);
                if (recipientSessions.Length == 0)
                {
                    this.logger.Debug(
                        "Recipient {0} went offline again before the queued-notification delay elapsed; leaving {1} notification(s) queued.",
                        recipientUserId,
                        pending.Count);
                    return;
                }

                foreach (var record in pending)
                {
                    await this.SendToSessionsAsync(recipientSessions, record.MessageText).ConfigureAwait(false);
                }

                await this.pendingNotificationStore
                    .RemoveAsync(recipientUserId, pending.Select(record => record.NotificationId))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Plugin shutdown; the records are still on disk for next time.
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("Error delivering queued recommendation notification(s) for user {0}", ex, recipientUserId);
            }
        }
    }
}