namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorStartVm
{
    public string Query { get; set; } = string.Empty;

    public bool HasSearched { get; set; }

    public int Page { get; set; } = 1;

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }

    public List<VideoSponsorListingRow> Results { get; set; } = new ();
}
