using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.SponsoredListing
{
    public enum CheckoutWaitingStatus
    {
        /// <summary>Someone else is currently checking out this slot; it may open up when their hold expires.</summary>
        Reserved = 0,

        /// <summary>The slot is now open — the buyer can continue their checkout.</summary>
        Available = 1,

        /// <summary>The slot was purchased by someone else; it's gone (send them to the waitlist).</summary>
        Sold = 2,
    }

    public class CheckoutWaitingViewModel
    {
        public CheckoutWaitingStatus Status { get; set; }

        public SponsorshipType SponsorshipType { get; set; }

        public int DirectoryEntryId { get; set; }

        public int? SelectedOfferId { get; set; }

        /// <summary>Human label of the placement being waited on, e.g. "Category: Businesses".</summary>
        public string PlacementLabel { get; set; } = string.Empty;

        /// <summary>When the current hold expires (Reserved only).</summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>Whole minutes left until the hold expires (Reserved only; floored at 0).</summary>
        public int MinutesLeft { get; set; }

        /// <summary>Self URL — used by the meta refresh and the manual "Check again" button.</summary>
        public string RefreshUrl { get; set; } = string.Empty;

        /// <summary>Where to resume checkout once the slot is Available.</summary>
        public string ContinueUrl { get; set; } = string.Empty;

        /// <summary>Where to join the waitlist when the slot is Sold.</summary>
        public string WaitlistUrl { get; set; } = string.Empty;
    }
}
