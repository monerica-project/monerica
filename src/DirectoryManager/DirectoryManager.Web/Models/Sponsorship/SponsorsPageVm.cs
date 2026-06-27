using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Models.Sponsorship
{
    /// <summary>
    /// Backing model for the public "/sponsors" page: who is sponsoring now and
    /// until when, the recent paid activity, the countdown to the next opening,
    /// and the current waitlist size (to drive people to join).
    /// </summary>
    public class SponsorsPageVm
    {
        public DateTime CurrentUtc { get; set; } = DateTime.UtcNow;

        public List<CurrentSponsorItemVm> CurrentSponsors { get; set; } = new ();

        public RecentPaidVm? RecentPaid { get; set; }

        public int MainWaitlistCount { get; set; }

        public int MaxSlots { get; set; }

        public int ActiveCount { get; set; }

        public bool IsFull => this.ActiveCount >= this.MaxSlots;

        public int Openings => Math.Max(0, this.MaxSlots - this.ActiveCount);

        /// <summary>Countdown to the next main-sponsor opening (set only when full).</summary>
        public ListingInventoryModel? MainSponsorInventory { get; set; }

        /// <summary>Price ranges per placement type (Main / Category / Subcategory).</summary>
        public List<SponsorshipPricingSummaryVm> PricingSummaries { get; set; } = new ();
    }
}
