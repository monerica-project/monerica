namespace DirectoryManager.Web.Models.Sponsorship
{
    public class WaitlistPublicRowVm
    {
        public string ListingName { get; set; } = "";
        public string ListingUrl { get; set; } = "";
        public DateTime JoinedUtc { get; set; }

        /// <summary>True when this listing could buy a sponsorship today.</summary>
        public bool IsEligible { get; set; }

        /// <summary>"Yes", "Needs verification", or when it will be (time listed).</summary>
        public string Eligibility { get; set; } = string.Empty;
    }
}
