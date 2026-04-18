namespace DirectoryManager.Web.Models.Sponsorship
{
    public class SponsorshipWaitlistsOverviewVm
    {
        public int TotalCount { get; set; }

        public SponsorshipWaitlistSectionVm? Main { get; set; }

        public List<SponsorshipWaitlistSectionVm> Categories { get; set; } = new ();

        public List<SponsorshipWaitlistSectionVm> Subcategories { get; set; } = new ();
    }
}
