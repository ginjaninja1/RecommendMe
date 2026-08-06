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

        public IReadOnlyList<BaseItem> Search(User searchingUser, string searchTerm, IReadOnlyList<string> allowedMediaTypes, int limit = 25)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || allowedMediaTypes.Count == 0)
            {
                return System.Array.Empty<BaseItem>();
            }

            var query = new InternalItemsQuery(searchingUser)
            {
                SearchTerm = searchTerm,
                IncludeItemTypes = allowedMediaTypes.ToArray(),
                Recursive = true,
                IsVirtualItem = false,
                Limit = limit
            };

            return this.libraryManager.GetItemList(query);
        }
    }
}
