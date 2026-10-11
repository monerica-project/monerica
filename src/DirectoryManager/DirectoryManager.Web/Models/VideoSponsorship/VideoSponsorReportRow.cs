namespace DirectoryManager.Web.Models.VideoSponsorship;

// Admin: one line of a video-sponsorship revenue breakdown (a month, or a video type).
public class VideoSponsorReportRow
{
    public string Label { get; set; } = string.Empty;

    public int Orders { get; set; }

    public int Videos { get; set; }

    public decimal RevenueUsd { get; set; }

    public decimal DemoCostsUsd { get; set; }

    public decimal DiscountsUsd { get; set; }

    public decimal PaidXmr { get; set; }
}
