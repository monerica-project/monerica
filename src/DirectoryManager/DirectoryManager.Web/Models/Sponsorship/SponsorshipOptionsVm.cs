using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipOptionsVm
    {
        public int DirectoryEntryId { get; set; }
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
        public string DirectoryEntryKey { get; set; } = "";
        public string DirectoryStatus { get; set; } = "";

        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string SubcategoryName { get; set; } = "";

        public bool CanAdvertise { get; set; }
        public List<string> IneligibilityReasons { get; set; } = new ();
        public bool ShowSubscribedBanner { get; set; }

        public SponsorshipTypeOptionVm Main { get; set; } = new ();
        public SponsorshipTypeOptionVm Category { get; set; } = new ();
        public SponsorshipTypeOptionVm Subcategory { get; set; } = new ();

        public WaitlistBoardVm WaitlistBoard { get; set; } = new ();
        public RecentPaidVm RecentPaid { get; set; } = new ();
    }
}
