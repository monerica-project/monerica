using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class SubcategorySponsorModel
    {
        public int SubCategoryId { get; set; }

        public int TotalActiveSubCategoryListings { get; set; }

        // When true, this subcategory has sponsorship turned off — hide the "Advertise here" CTA.
        public bool SponsorshipDisabled { get; set; }

        public DirectoryEntryViewModel? DirectoryEntry { get; set; }
    }
}
