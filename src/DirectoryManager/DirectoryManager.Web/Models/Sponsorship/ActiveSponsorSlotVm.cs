namespace DirectoryManager.Web.Models.Sponsorship
{
    public class ActiveSponsorSlotVm
    {
        public int DirectoryEntryId { get; set; }
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
        public DateTime CampaignEndUtc { get; set; }
        public bool IsYou { get; set; }
    }
}
