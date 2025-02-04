using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class SubCategorySponsorModel
    {
        public int SubCategoryId { get; set; }

        public int TotalActiveSubCategoryListings { get; set; }

        public DirectoryEntryViewModel? DirectoryEntry { get; set; }
    }
}
