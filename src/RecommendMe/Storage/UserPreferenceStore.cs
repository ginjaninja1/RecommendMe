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
    /// Persists each user's Account-tab opt-in/out preferences to
    /// user-preferences.json.
    /// </summary>
    internal class UserPreferenceStore
    {
        private readonly JsonFileRepository<UserPreferenceCollection> repository;

        public UserPreferenceStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<UserPreferenceCollection>(
                RecommendMeDataPaths.UserPreferencesFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public async Task<UserReceivePreferences> GetForUserAsync(long userId)
        {
            var data = await this.repository.ReadAsync().ConfigureAwait(false);
            return data.Users.FirstOrDefault(u => u.UserId == userId)
                   ?? new UserReceivePreferences { UserId = userId };
        }

        public Task SaveForUserAsync(UserReceivePreferences preferences)
        {
            return this.repository.MutateAsync(data =>
            {
                var existing = data.Users.FirstOrDefault(u => u.UserId == preferences.UserId);
                if (existing != null)
                {
                    data.Users.Remove(existing);
                }

                data.Users.Add(preferences);
            });
        }
    }
}
