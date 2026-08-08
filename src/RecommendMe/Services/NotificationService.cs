using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;

namespace RecommendMe.Services
{
    /// <summary>Sends best-effort recommendation notifications to active recipient sessions.</summary>
    internal class NotificationService
    {
        private readonly ISessionManager sessionManager;
        private readonly ILogger logger;

        public NotificationService(ISessionManager sessionManager, ILogger logger)
        {
            this.sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NotifyRecommendationReceivedAsync(
            User recipient,
            User sender,
            string itemName,
            string mediaType)
        {
            var command = new MessageCommand
            {
                Header = "New Recommendation",
                Text = $"{sender.Name} recommended the {mediaType.ToLowerInvariant()} \"{itemName}\"",
                TimeoutMs = 8000
            };

            var sends = this.sessionManager.Sessions
                .Where(session => session.UserInternalId == recipient.InternalId)
                .Select(session => this.SendAsync(session.Id, command));

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
    }
}
