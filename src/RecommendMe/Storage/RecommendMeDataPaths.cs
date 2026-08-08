using System.IO;
using MediaBrowser.Common.Configuration;

namespace RecommendMe.Storage
{
    /// <summary>
    /// Resolves the single data directory all RecommendMe JSON files live
    /// under: ProgramData\data\RecommendMe\.
    /// </summary>
    internal static class RecommendMeDataPaths
    {
        private const string FolderName = "RecommendMe";

        public static string GetDataDirectory(IApplicationPaths applicationPaths)
        {
            return Path.Combine(applicationPaths.DataPath, FolderName);
        }

        public static string AdminSettingsFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "admin-settings.json");

        public static string RecommendationsFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "recommendations.json");

        public static string UserPreferencesFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "user-preferences.json");

        public static string CollectionRegistryFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "collection-registry.json");

        public static string CollectionCollagesFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "collection-collages.json");

        public static string CollectionCollageImagesDirectory(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "collection-collage-images");

        public static string PendingNotificationsFile(IApplicationPaths applicationPaths) =>
            Path.Combine(GetDataDirectory(applicationPaths), "pending-notifications.json");
    }
}