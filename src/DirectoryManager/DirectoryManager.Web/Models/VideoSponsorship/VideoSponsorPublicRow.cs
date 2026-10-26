namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorPublicRow
{
    public string ListingName { get; set; } = string.Empty;

    public string DirectoryEntryKey { get; set; } = string.Empty;

    public string VideoTitle { get; set; } = string.Empty;

    public string OfferName { get; set; } = string.Empty;

    public DateTime? PaidUtc { get; set; }

    public decimal AmountUsd { get; set; }

    public string? VideoUrl { get; set; }

    public string? BlogArticleUrl { get; set; }

    public DateTime? CompletedUtc { get; set; }
}
