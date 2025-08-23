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
    }
}