using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class TaggedEntriesViewModel
    {
        public Tag Tag { get; set; } = default!;
        public PagedResult<DirectoryEntryViewModel> PagedEntries { get; set; } = default!;
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
}
