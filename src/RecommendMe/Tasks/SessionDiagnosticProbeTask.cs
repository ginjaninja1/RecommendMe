using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Tasks;

namespace RecommendMe.Tasks
{
    /// <summary>
    /// Diagnostic-only task with no effect on plugin state. Run it manually
    /// from Scheduled Tasks in the Emby dashboard - before/after a logon,
    /// during playback, after a disconnect, etc. - and read the resulting
    /// log lines to see what ISessionManager.Sessions actually reports at
    /// that moment. Not wired to anything else; safe to remove once the
    /// notification-delivery behaviour it exists to sanity-check is
    /// confirmed working as intended.
    /// </summary>
    public class SessionDiagnosticsProbeTask : IScheduledTask
    {
        public string Name => "RecommendMe - Session diagnostics probe";
        public string Key => "RecommendMeSessionDiagnosticsProbe";
        public string Description => "Logs the current ISessionManager.Sessions state (online status, activity, now-playing) for manual inspection. Diagnostic only.";
        public string Category => "GinjaNinja Tools";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var plugin = Plugin.Instance;
            if (plugin == null)
            {
                progress?.Report(100);
                return Task.CompletedTask;
            }

            var sessionManager = plugin.SessionManager;
            var sessions = new List<SessionInfo>(sessionManager.Sessions);

            plugin.Logger.Info("Session diagnostics probe - {0} session(s) currently reported.", sessions.Count);

            for (var i = 0; i < sessions.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var session = sessions[i];
                plugin.Logger.Info(
                    "Session {0}/{1}: Id={2}, UserId={3}, UserName={4}, Client={5}, DeviceName={6}, "
                    + "IsActive={7}, LastActivityDate={8:o}, NowPlayingItem={9}",
                    i + 1,
                    sessions.Count,
                    session.Id,
                    session.UserInternalId,
                    session.UserName,
                    session.Client,
                    session.DeviceName,
                    session.IsActive,
                    session.LastActivityDate,
                    session.NowPlayingItem?.Name ?? "(none)");

                progress?.Report(sessions.Count == 0 ? 100 : (double)(i + 1) / sessions.Count * 100);
            }

            progress?.Report(100);
            return Task.CompletedTask;
        }
    }
}