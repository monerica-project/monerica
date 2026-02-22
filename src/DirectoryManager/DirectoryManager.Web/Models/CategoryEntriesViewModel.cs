using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class CategoryEntriesViewModel
    {
        // from your existing CategoryViewModel
        public int CategoryId { get; set; }
        public string CategoryKey { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string? Description { get; set; }
        public string? Note { get; set; }
        public string? MetaDescription { get; set; }

        // paging
        public PagedResult<DirectoryEntryViewModel> PagedEntries { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public HashSet<int> SponsoredDirectoryEntryIds { get; set; }
    }
}
