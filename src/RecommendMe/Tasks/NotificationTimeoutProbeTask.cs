using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;

namespace RecommendMe.Tasks
{
    /// <summary>
    /// Diagnostic-only task with no effect on plugin state. Sends every
    /// currently-active session a sequence of toast notifications with
    /// different TimeoutMs values, so the client-side on-screen duration
    /// can be observed and timed manually against what was requested.
    /// Message text states its own requested duration and when the next
    /// message is due, so the tester doesn't need to watch a clock
    /// separately. Not wired to anything else; safe to remove once
    /// client-side toast timeout behaviour is confirmed.
    /// </summary>
    public class NotificationTimeoutProbeTask : IScheduledTask
    {
        private static readonly (double DurationSeconds, int DelayToNextSeconds)[] Steps =
        {
            (3.3, 10),
            (10.0, 20),
            (30.0, 0)
        };

        public string Name => "RecommendMe - Notification timeout probe";
        public string Key => "RecommendMeNotificationTimeoutProbe";
        public string Description => "Sends all online sessions a sequence of toast notifications with different TimeoutMs values, for manual client-side timeout observation. Diagnostic only.";
        public string Category => "GinjaNinja Tools";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                progress?.Report(100);
                return;
            }

            var sessionManager = plugin.SessionManager;

            plugin.Logger.Info("Notification timeout probe - waiting 10s before first message.");
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < Steps.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (durationSeconds, delayToNextSeconds) = Steps[i];
                var sessions = sessionManager.Sessions
                    .Where(session => session.IsActive)
                    .ToArray();

                var nextText = delayToNextSeconds > 0
                    ? $"Next message in {delayToNextSeconds}s."
                    : "Final message.";
                var text = $"Message {i + 1} - Duration {durationSeconds}s. {nextText}";

                plugin.Logger.Info(
                    "Notification timeout probe - sending message {0}/{1} ('{2}') to {3} active session(s).",
                    i + 1,
                    Steps.Length,
                    text,
                    sessions.Length);

                var command = new MessageCommand
                {
                    Header = "Timeout Probe",
                    Text = text,
                    TimeoutMs = (long)(durationSeconds * 1000)
                };

                var sends = sessions.Select(session => this.SendAsync(plugin, sessionManager, session.Id, command));
                await Task.WhenAll(sends).ConfigureAwait(false);

                progress?.Report((double)(i + 1) / Steps.Length * 100);

                if (delayToNextSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayToNextSeconds), cancellationToken).ConfigureAwait(false);
                }
            }

            progress?.Report(100);
        }

        private async Task SendAsync(Plugin plugin, ISessionManager sessionManager, string sessionId, MessageCommand command)
        {
            try
            {
                await sessionManager.SendMessageCommand(
                    controllingSessionId: null,
                    sessionId: sessionId,
                    command: command,
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                plugin.Logger.ErrorException("Unable to send timeout probe notification to session {0}", ex, sessionId);
            }
        }
    }
}