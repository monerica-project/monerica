using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VideoSponsorships;

namespace DirectoryManager.Web.Models.VideoSponsorship;

// One model for every step of the video-sponsor checkout (type -> videos -> details).
// The flow is stateless: each step carries the listing, type and chosen videos forward and
// the server re-validates all of it every time.
public class VideoSponsorCheckoutVm
{
    public int DirectoryEntryId { get; set; }

    public string ListingName { get; set; } = string.Empty;

    public string DirectoryEntryKey { get; set; } = string.Empty;

    // Type step: every enabled offer. Later steps: just the chosen one in SelectedOffer.
    public List<VideoSponsorOfferRow> Offers { get; set; } = new ();

    public VideoSponsorOfferRow? SelectedOffer { get; set; }

    // Videos step: what can be picked right now.
    public List<VideoIdea> AvailableVideos { get; set; } = new ();

    // Chosen videos (ids as a comma list for links/hidden fields).
    public List<VideoIdea> SelectedVideos { get; set; } = new ();

    public string SelectedVideoIds { get; set; } = string.Empty;

    // Existing Sponsor Perk.
    public decimal DiscountPercent { get; set; }

    public SponsorshipType? DiscountBasisTier { get; set; }

    // Totals for the details step.
    public int Quantity { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    // The discounted sponsorship price on its own (before any demo cost).
    public decimal SponsorshipTotal { get; set; }

    // Buyer-entered cost of demonstrating their product (0 = none / $100 or less).
    public decimal DemoCostUsd { get; set; }

    // What the buyer typed, kept so the form can be shown again as entered.
    public string? DemoCostInput { get; set; }

    // Set when the total on screen was just recalculated and needs the buyer's confirmation.
    public string? Notice { get; set; }

    // Sponsorship price plus demo cost: what the invoice is for.
    public decimal Total { get; set; }

    // Details form.
    public string? Notes { get; set; }

    public string? RefundAddress { get; set; }

    public string? Email { get; set; }

    public string ChannelUrl { get; set; } = string.Empty;

    public bool CheckoutAvailable { get; set; }

    public int DeliveryDays { get; set; }

    public string? Error { get; set; }
}
