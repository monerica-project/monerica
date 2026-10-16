using Microsoft.AspNetCore.Http;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Builds the "page 1" URL for query-string paged pages (search, filter) so a request past
    /// the last page can be redirected to page 1, matching the category/subcategory/tag pages.
    /// The path is fixed by the caller (never taken from the request) so this is not an open
    /// redirect; every other query parameter is preserved.
    /// </summary>
    public static class PagingRedirectHelper
    {
        public static string FirstPageUrl(string path, IQueryCollection? query)
        {
            if (query is null || query.Count == 0)
            {
                return path;
            }

            var kept = query
                .Where(kv => !string.Equals(kv.Key, "page", StringComparison.OrdinalIgnoreCase))
                .SelectMany(kv => kv.Value.Select(v => new KeyValuePair<string, string?>(kv.Key, v)))
                .ToList();

            return kept.Count == 0 ? path : path + QueryString.Create(kept).ToUriComponent();
        }
    }
}
