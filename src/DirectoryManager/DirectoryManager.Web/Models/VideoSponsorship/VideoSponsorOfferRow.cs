namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorOfferRow
{
    public int VideoSponsorOfferId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool RequiresVideoSelection { get; set; }

    // Price for one video, before and after the Existing Sponsor Perk.
    public decimal Price { get; set; }

    public decimal DiscountedPrice { get; set; }
}
