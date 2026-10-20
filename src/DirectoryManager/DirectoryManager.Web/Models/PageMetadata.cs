using Microsoft.AspNetCore.Http;

namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// Typed, per-request page settings that a view (or its controller) sets and the layout
    /// reads: the &lt;title&gt;, meta tags, canonical URL, and the sidebar's current category.
    /// One instance per request, shared by the controller, the view, its partials and the layout.
    /// </summary>
    public sealed class PageMetadata
    {
        private const string ItemsKey = "__PageMetadata";

        /// <summary>The page &lt;title&gt;, also shown as the page heading on many pages.</summary>
        public string? Title { get; set; }

        /// <summary>The meta description (also used for twitter:description).</summary>
        public string? MetaDescription { get; set; }

        /// <summary>Extra raw &lt;meta&gt; markup for the head (e.g. the noindex tag).</summary>
        public string? MetaContent { get; set; }

        /// <summary>The canonical URL for the &lt;link rel="canonical"&gt; tag.</summary>
        public string? CanonicalUrl { get; set; }

        /// <summary>False adds a robots noindex tag; null/true leaves the page indexable.</summary>
        public bool? IsIndexable { get; set; }

        /// <summary>Category key the sidebar highlights as current, if any.</summary>
        public string? CurrentCategoryKey { get; set; }

        /// <summary>Subcategory key the sidebar highlights as current, if any.</summary>
        public string? CurrentSubCategoryKey { get; set; }

        /// <summary>The request's page settings (created on first use). A null context (unit tests) gets a throwaway instance.</summary>
        public static PageMetadata For(HttpContext? httpContext)
        {
            if (httpContext is null)
            {
                return new PageMetadata();
            }

            if (httpContext.Items.TryGetValue(ItemsKey, out var existing) && existing is PageMetadata page)
            {
                return page;
            }

            var created = new PageMetadata();
            httpContext.Items[ItemsKey] = created;
            return created;
        }
    }
}
