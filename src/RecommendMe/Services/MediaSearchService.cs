using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

namespace RecommendMe.Services
{
    /// <summary>
    /// Thin wrapper over ILibraryManager for the one query the Recommend page
    /// needs: "items of these types whose name matches this search term,
    /// visible to this user".
    /// </summary>
    public class MediaSearchService
    {
        private readonly ILibraryManager libraryManager;
        private readonly ILogger logger;

        public MediaSearchService(ILibraryManager libraryManager, ILogger logger)
        {
            this.libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<BaseItem> Search(User searchingUser, string searchTerm, IReadOnlyList<string> allowedMediaTypes, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || allowedMediaTypes == null || allowedMediaTypes.Count == 0)
            {
                this.logger.Warn(
                    "RecommendMe: media search skipped - user={0} ({1}), term='{2}', termLength={3}, allowedTypeCount={4}.",
                    searchingUser?.Name ?? "(null)",
                    searchingUser?.InternalId ?? 0,
                    LogValue(searchTerm),
                    searchTerm?.Length ?? 0,
                    allowedMediaTypes?.Count ?? 0);
                return System.Array.Empty<BaseItem>();
            }

            var effectiveMediaTypes = allowedMediaTypes
                .Where(t => t != Models.RecommendableMediaTypes.BoxSet && t != Models.RecommendableMediaTypes.Season)
                .ToArray();

            var regularMediaTypes = effectiveMediaTypes
                .Where(t => t != Models.RecommendableMediaTypes.Person)
                .ToArray();
            var includePeople = effectiveMediaTypes.Contains(Models.RecommendableMediaTypes.Person);

            this.logger.Info(
                "RecommendMe: media search query - user={0} ({1}), term='{2}', termLength={3}, configuredTypes=[{4}], regularTypes=[{5}] with isVirtualItem=false, queryVirtualPeople={6}, limit={7}.",
                searchingUser?.Name ?? "(null)",
                searchingUser?.InternalId ?? 0,
                LogValue(searchTerm),
                searchTerm.Length,
                string.Join(", ", allowedMediaTypes),
                string.Join(", ", regularMediaTypes),
                includePeople,
                limit);

            var stopwatch = Stopwatch.StartNew();
            var regularResults = this.SearchItems(searchingUser, searchTerm, regularMediaTypes, false, limit);
            var personResults = includePeople
                ? this.SearchItems(
                    searchingUser,
                    searchTerm,
                    new[] { Models.RecommendableMediaTypes.Person },
                    true,
                    limit)
                : System.Array.Empty<BaseItem>();
            var results = regularResults
                .Concat(personResults)
                .GroupBy(item => item.InternalId)
                .Select(group => group.First())
                .OrderByDescending(item => string.Equals(item.Name, searchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToArray();
            stopwatch.Stop();

            var resultSummary = string.Join(
                "; ",
                results.Take(10).Select(item => $"{item.GetType().Name}:{LogValue(item.Name)} ({item.InternalId})"));

            this.logger.Info(
                "RecommendMe: media search result - term='{0}', regularCount={1}, personCount={2}, returnedCount={3}, elapsedMs={4}, items=[{5}].",
                LogValue(searchTerm),
                regularResults.Length,
                personResults.Length,
                results.Length,
                stopwatch.ElapsedMilliseconds,
                resultSummary);

            if (results.Length == 0)
            {
                this.logger.Warn(
                    "RecommendMe: media search returned no items - term='{0}', PersonConfigured={1}, PersonQueriedAsVirtual={2}. Check that Emby exposes matching items to user {3} ({4}).",
                    LogValue(searchTerm),
                    allowedMediaTypes.Contains(Models.RecommendableMediaTypes.Person),
                    includePeople,
                    searchingUser?.Name ?? "(null)",
                    searchingUser?.InternalId ?? 0);
            }

            return results;
        }

        private static string LogValue(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", "\\r").Replace("\n", "\\n");

        private BaseItem[] SearchItems(User user, string searchTerm, string[] itemTypes, bool isVirtualItem, int limit)
        {
            if (itemTypes.Length == 0)
            {
                return System.Array.Empty<BaseItem>();
            }

            var query = new InternalItemsQuery(user)
            {
                NameContains = searchTerm,
                IncludeItemTypes = itemTypes,
                Recursive = true,
                IsVirtualItem = isVirtualItem,
                Limit = limit
            };

            return this.libraryManager.GetItemList(query).ToArray();
        }

        public IReadOnlyList<BaseItem> GetChildren(User user, BaseItem parent)
        {
            var type = parent.GetType().Name;
            var query = new InternalItemsQuery(user) { Recursive = true, IsVirtualItem = false };

            if (type == Models.RecommendableMediaTypes.Series)
            {
                query.IncludeItemTypes = new[] { Models.RecommendableMediaTypes.Season };
                query.SeriesIds = new[] { parent.InternalId };
                query.Recursive = false;
            }
            else if (type == Models.RecommendableMediaTypes.Season)
            {
                query.IncludeItemTypes = new[] { Models.RecommendableMediaTypes.Episode };
                query.ParentIds = new[] { parent.InternalId };
            }
            else if (type == Models.RecommendableMediaTypes.MusicArtist)
            {
                query.IncludeItemTypes = new[] { Models.RecommendableMediaTypes.MusicAlbum };
                query.ArtistIds = new[] { parent.InternalId };
                query.GroupByAlbumId = true;
            }
            else if (type == Models.RecommendableMediaTypes.MusicAlbum)
            {
                query.IncludeItemTypes = new[] { Models.RecommendableMediaTypes.Song };
                query.AlbumIds = new[] { parent.InternalId };
            }
            else
            {
                return System.Array.Empty<BaseItem>();
            }

            return this.libraryManager.GetItemList(query);
        }
    }
}
