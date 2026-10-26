namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorExamplesVm
{
    public List<VideoSponsorPublicRow> Rows { get; set; } = new ();

    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }
}
