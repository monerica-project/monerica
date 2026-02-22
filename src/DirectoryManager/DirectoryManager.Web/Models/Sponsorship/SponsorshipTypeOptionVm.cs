using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Controllers;

namespace DirectoryManager.Web.Models.Sponsorship
{

    public class SponsorshipTypeOptionVm
    {
        public SponsorshipType SponsorshipType { get; set; }
        public string ScopeLabel { get; set; } = "";

        public bool IsExtension { get; set; }
        public bool IsAvailableNow { get; set; }

        public int PoolActiveCount { get; set; }
        public int PoolMaxSlots { get; set; }
        public bool PoolHasCheckoutLock { get; set; }

        public bool BlockedByMainSubcategoryCap { get; set; }
        public DateTime? NextOpeningForMainSubcategoryCapUtc { get; set; }

        public List<SponsorshipOfferVm> Offers { get; set; } = new ();
        public WaitlistPanelVm Waitlist { get; set; } = new ();

        public List<ActiveSponsorSlotVm> ActiveSlots { get; set; } = new ();
        public DateTime? YourActiveUntilUtc { get; set; }

    }
}
