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
    }
}