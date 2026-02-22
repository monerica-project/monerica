namespace DirectoryManager.Web.Models.Sponsorship
{

    public class WaitlistPublicRowVm
    {
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
        public DateTime JoinedUtc { get; set; }
    }
}
