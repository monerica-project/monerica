using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipIndexVm
    {
        // Search
        public string? Query { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool HasSearched { get; set; }

        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;

        public List<SponsorshipSearchItemVm> Results { get; set; } = new ();

        // Page widgets
        public WaitlistBoardVm? WaitlistBoard { get; set; }
        public RecentPaidVm? RecentPaid { get; set; }

        // ==========================================
        // Main Sponsor availability (for /sponsorship)
        // ==========================================
        public bool MainSponsorIsOpen { get; set; }
        public int MainSponsorActiveCount { get; set; }
        public int MainSponsorMaxSlots { get; set; }

        // When full, reuse the same model style as your other page
        public ListingInventoryModel? MainSponsorInventory { get; set; }

        public List<SponsorshipPricingSummaryVm> PricingSummaries { get; set; } = new ();
    }
}