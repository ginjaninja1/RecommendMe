using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using RecommendMe.Models;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Persists <see cref="AdminSettings"/> to admin-settings.json.
    /// </summary>
    internal class AdminSettingsStore
    {
        private readonly JsonFileRepository<AdminSettings> repository;

        public AdminSettingsStore(
            IApplicationPaths applicationPaths,
            IFileSystem fileSystem,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            this.repository = new JsonFileRepository<AdminSettings>(
                RecommendMeDataPaths.AdminSettingsFile(applicationPaths),
                fileSystem,
                jsonSerializer,
                logger);
        }

        public Task<AdminSettings> GetAsync() => this.repository.ReadAsync();

        public Task MutateAsync(System.Action<AdminSettings> mutate) => this.repository.MutateAsync(mutate);
    }
}
