using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
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

        public MediaSearchService(ILibraryManager libraryManager)
        {
            this.libraryManager = libraryManager;
        }

        public IReadOnlyList<BaseItem> Search(User searchingUser, string searchTerm, IReadOnlyList<string> allowedMediaTypes, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || allowedMediaTypes.Count == 0)
            {
                return System.Array.Empty<BaseItem>();
            }

            var query = new InternalItemsQuery(searchingUser)
            {
                NameContains = searchTerm,
                IncludeItemTypes = allowedMediaTypes
                    .Where(t => t != Models.RecommendableMediaTypes.BoxSet && t != Models.RecommendableMediaTypes.Season)
                    .ToArray(),
                Recursive = true,
                IsVirtualItem = false,
                Limit = limit
            };

            return this.libraryManager.GetItemList(query);
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
