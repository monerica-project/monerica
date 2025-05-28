using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class SubcategorySponsorModel
    {
        public int SubCategoryId { get; set; }

        public int TotalActiveSubCategoryListings { get; set; }

        public DirectoryEntryViewModel? DirectoryEntry { get; set; }
    }
}
