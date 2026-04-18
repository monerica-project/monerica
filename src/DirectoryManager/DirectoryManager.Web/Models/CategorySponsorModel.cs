using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    /// <summary>
    /// View model for a category-level sponsored listing.
    /// </summary>
    public class CategorySponsorModel
    {
        /// <summary>
        /// The ID of the category being sponsored.
        /// </summary>
        public int CategoryId { get; set; }

        /// <summary>
        /// How many active listings exist in this category (including sponsored).
        /// </summary>
        public int TotalActiveCategoryListings { get; set; }

        /// <summary>
        /// If there's an active sponsored listing for this category, its directory entry view model.
        /// </summary>
        public DirectoryEntryViewModel? DirectoryEntry { get; set; }
    }
}
