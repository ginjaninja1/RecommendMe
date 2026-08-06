using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;

namespace RecommendMe.Services
{
    /// <summary>
    /// Fires an on-screen toast (MessageCommand over an active session) to a
    /// recipient the moment a recommendation is sent to them. Best-effort:
    /// if the recipient has no active session, this is a silent no-op - the
    /// recommendation itself is already persisted and will show up in their
    /// collection/history regardless.
    /// </summary>
    public class NotificationService
    {
        private readonly ISessionManager sessionManager;

        public NotificationService(ISessionManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public void NotifyRecommendationReceived(User recipient, User sender, string itemName, string mediaType)
        {
            var header = "New Recommendation";
            var text = $"{sender.Name} recommended the {mediaType.ToLowerInvariant()} \"{itemName}\"";

            var command = new MessageCommand
            {
                Header = header,
                Text = text,
                TimeoutMs = 8000
            };

            var recipientSessions = this.sessionManager.Sessions
                .Where(s => s.UserInternalId == recipient.InternalId)
                .ToList();

            foreach (var session in recipientSessions)
            {
                // Fire-and-forget: a notification failing to reach one session
                // must never block or fail the recommendation itself.
                _ = this.sessionManager.SendMessageCommand(
                    controllingSessionId: null,
                    sessionId: session.Id,
                    command: command,
                    cancellationToken: CancellationToken.None);
            }
        }
    }
}
