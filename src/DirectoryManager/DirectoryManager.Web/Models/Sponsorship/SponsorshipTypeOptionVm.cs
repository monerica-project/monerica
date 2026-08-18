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

        /// <summary>Cross-tier existing-sponsor perk percent that applies to buying this placement (0 = none).</summary>
        public decimal ExistingSponsorPerkPercent { get; set; }

        /// <summary>The active tier that grants the existing-sponsor perk (for banner copy). Null when no discount.</summary>
        public SponsorshipType? ExistingSponsorPerkBasisTier { get; set; }
    }
}
