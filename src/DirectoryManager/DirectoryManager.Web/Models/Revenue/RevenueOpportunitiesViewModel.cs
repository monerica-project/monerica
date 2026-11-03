namespace DirectoryManager.Web.Models.Revenue
{
    public class RevenueOpportunitiesViewModel
    {
        public int Days { get; set; }

        public List<AbandonedCheckoutRow> Abandoned { get; set; } = new ();

        public List<RenewalRow> Expiring { get; set; } = new ();

        public List<RenewalRow> Lapsed { get; set; } = new ();

        public List<ProspectRow> Prospects { get; set; } = new ();

        public DateTime? ViewsUpdatedUtc { get; set; }
    }
}
