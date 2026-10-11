namespace DirectoryManager.Web.Models.VideoSponsorship;

// Admin: one row per listing that has paid for video sponsorship.
public class VideoSponsorSummaryRow
{
    public int DirectoryEntryId { get; set; }

    public string ListingName { get; set; } = string.Empty;

    public string DirectoryEntryKey { get; set; } = string.Empty;

    public int Orders { get; set; }

    public int Videos { get; set; }

    public int VideosDelivered { get; set; }

    public decimal TotalPaidUsd { get; set; }

    public decimal DemoCostsUsd { get; set; }

    public DateTime? FirstPaidUtc { get; set; }

    public DateTime? LastPaidUtc { get; set; }

    public string? Email { get; set; }
}
