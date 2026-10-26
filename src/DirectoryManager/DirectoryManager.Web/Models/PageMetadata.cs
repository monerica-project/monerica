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

        /// <summary>
        /// The exact &lt;title&gt; for search results, when it should differ from <see cref="Title"/>
        /// (which many pages also show as their heading). When null, the layout uses
        /// <see cref="Title"/> plus " | SiteName" if that still fits in 60 characters.
        /// </summary>
        public string? SeoTitle { get; set; }

        /// <summary>og:image / twitter:image for this page; null falls back to the site image.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>og:type; null means "website".</summary>
        public string? OgType { get; set; }

        /// <summary>JSON-LD documents (already serialized) the layout emits in the head.</summary>
        public List<string> StructuredData { get; } = new ();

        /// <summary>The meta description (also used for og:description and twitter:description).</summary>
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

        /// <summary>
        /// Clears everything a page set. Used by the error page: a re-executed request shares the
        /// original request's context, so a controller that set a canonical URL or title before
        /// returning 404 must not leak those into the error page.
        /// </summary>
        public void Clear()
        {
            this.Title = null;
            this.SeoTitle = null;
            this.ImageUrl = null;
            this.OgType = null;
            this.StructuredData.Clear();
            this.MetaDescription = null;
            this.MetaContent = null;
            this.CanonicalUrl = null;
            this.IsIndexable = null;
            this.CurrentCategoryKey = null;
            this.CurrentSubCategoryKey = null;
        }

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
