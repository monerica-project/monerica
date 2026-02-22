using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipWaitlistVm
    {
        public SponsorshipType SponsorshipType { get; set; }
        public int? TypeId { get; set; }
        public string ScopeLabel { get; set; } = "";

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public List<WaitlistPublicItemVm> Items { get; set; } = new ();
    }
}
