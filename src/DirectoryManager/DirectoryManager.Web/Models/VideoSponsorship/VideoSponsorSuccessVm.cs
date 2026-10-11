using DirectoryManager.Data.Models.VideoSponsorships;

namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorSuccessVm
{
    public VideoSponsorOrder Order { get; set; } = null!;

    public bool IsPaid { get; set; }

    public bool IsUnderPaid { get; set; }

    public int DeliveryDays { get; set; }
}
