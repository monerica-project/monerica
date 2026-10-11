namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorReportVm
{
    public decimal TotalRevenueUsd { get; set; }

    public decimal TotalDemoCostsUsd { get; set; }

    public decimal TotalDiscountsUsd { get; set; }

    public decimal TotalPaidXmr { get; set; }

    public int TotalOrders { get; set; }

    public int TotalVideos { get; set; }

    public int VideosDelivered { get; set; }

    public int VideosOutstanding { get; set; }

    public int VideosOverdue { get; set; }

    public List<VideoSponsorReportRow> ByMonth { get; set; } = new ();

    public List<VideoSponsorReportRow> ByType { get; set; } = new ();
}
