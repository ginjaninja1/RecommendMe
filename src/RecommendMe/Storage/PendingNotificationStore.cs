using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Persists recommendation notifications that couldn't be delivered
    /// immediately because the recipient had no active session. Entries are
    /// removed once a later SessionStarted event successfully delivers
    /// them; there is no delivery-receipt concept, so "removed" only means
    /// "a send was attempted while the recipient appeared online".
    /// </summary>
    internal class PendingNotificationStore
    {
        private readonly JsonFileRepository<PendingNotificationCollection> repository;

        public PendingNotificationStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<PendingNotificationCollection>(
                RecommendMeDataPaths.PendingNotificationsFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public Task AddAsync(PendingNotificationRecord record)
        {
            return this.repository.MutateAsync(data => data.Records.Add(record));
        }

        public async Task<List<PendingNotificationRecord>> GetForUserAsync(long recipientUserId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            return data.Records
                .Where(record => record.RecipientUserId == recipientUserId)
                .ToList();
        }

        public Task RemoveAsync(long recipientUserId, IEnumerable<string> notificationIds)
        {
            var idSet = new HashSet<string>(notificationIds);
            return this.repository.MutateAsync(data =>
                data.Records.RemoveAll(record =>
                    record.RecipientUserId == recipientUserId && idSet.Contains(record.NotificationId)));
        }
    }
}