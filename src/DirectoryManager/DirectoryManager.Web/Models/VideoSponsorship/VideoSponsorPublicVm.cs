namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorPublicVm
{
    public List<VideoSponsorOfferRow> Offers { get; set; } = new ();

    public List<VideoSponsorPublicRow> Sponsorships { get; set; } = new ();

    public string ChannelUrl { get; set; } = string.Empty;

    public string VideoSiteUrl { get; set; } = string.Empty;

    public int DeliveryDays { get; set; }
}
