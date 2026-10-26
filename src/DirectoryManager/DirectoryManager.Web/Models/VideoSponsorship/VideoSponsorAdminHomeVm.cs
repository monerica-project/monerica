namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorAdminHomeVm
{
    public bool IsEnabled { get; set; }

    public bool IsPaymentConfigured { get; set; }

    public string ChannelUrl { get; set; } = string.Empty;

    public string VideoSiteUrl { get; set; } = string.Empty;

    public string BlogUrl { get; set; } = string.Empty;

    public string WebhookUrl { get; set; } = string.Empty;

    public int QueuedVideos { get; set; }

    public int OpenToSponsor { get; set; }

    public int PaidOrders { get; set; }

    public int VideosOutstanding { get; set; }

    public int OrdersNeedingAttention { get; set; }
}
