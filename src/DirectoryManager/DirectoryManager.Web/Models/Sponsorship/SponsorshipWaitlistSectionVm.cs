using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipWaitlistSectionVm
    {
        public SponsorshipType SponsorshipType { get; set; }

        public int? TypeId { get; set; }

        public string ScopeLabel { get; set; } = string.Empty;

        public int TotalCount { get; set; }

        public string BrowseUrl { get; set; } = string.Empty;

        public List<WaitlistPublicItemVm> Items { get; set; } = new ();
    }
}
