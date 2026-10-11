namespace DirectoryManager.Web.Models.VideoSponsorship;

public class VideoSponsorListingRow
{
    public int DirectoryEntryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DirectoryEntryKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public bool CanSponsor { get; set; }

    public List<string> Reasons { get; set; } = new ();
}
