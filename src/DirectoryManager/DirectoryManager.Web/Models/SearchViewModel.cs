using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class SearchViewModel
    {
        public string Query { get; set; } = "";
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public IList<DirectoryEntryViewModel> Entries { get; set; } = Array.Empty<DirectoryEntryViewModel>();

        public bool ShowDonatePin { get; set; }

        /// <summary>
        /// Set when the typed query found nothing and these results are for a spelling
        /// correction of it (e.g. "trocadr" → "trocador").
        /// </summary>
        public string? CorrectedQuery { get; set; }

        /// <summary>A possible spelling fix to offer as a link when nothing was found.</summary>
        public string? DidYouMean { get; set; }

        /// <summary>Category/subcategory/tag/country pages the search names ("vpn" → VPNs &amp; Proxies).</summary>
        public IList<DirectoryManager.Data.Helpers.BrowseTarget> BrowseLinks { get; set; } = new List<DirectoryManager.Data.Helpers.BrowseTarget>();

        /// <summary>A site page for a navigational search ("advertise", "scam").</summary>
        public DirectoryManager.Data.Helpers.BrowseTarget? Shortcut { get; set; }

        /// <summary>Closest categories, shown when nothing was found.</summary>
        public IList<DirectoryManager.Data.Helpers.BrowseTarget> ClosestLinks { get; set; } = new List<DirectoryManager.Data.Helpers.BrowseTarget>();

        /// <summary>Admin-only (?debug=1): why each result ranks where it does, by listing id.</summary>
        public IDictionary<int, string>? DebugExplanations { get; set; }
    }
}