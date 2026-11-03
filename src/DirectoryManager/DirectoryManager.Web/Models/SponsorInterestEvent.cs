namespace DirectoryManager.Web.Models
{
    public class SponsorInterestEvent
    {
        public DateTime When { get; set; }

        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Placement { get; set; } = string.Empty;

        public string Step { get; set; } = string.Empty;
    }
}
