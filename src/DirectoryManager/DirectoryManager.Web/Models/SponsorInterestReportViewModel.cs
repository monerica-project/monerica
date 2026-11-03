namespace DirectoryManager.Web.Models
{
    public class SponsorInterestReportViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int TotalLooks { get; set; }

        public int ListingCount { get; set; }

        public int StartedPaymentListings { get; set; }

        public int PaidListings { get; set; }

        public List<SponsorInterestWeek> Weeks { get; set; } = new ();

        public List<SponsorInterestListingRow> Listings { get; set; } = new ();

        public List<SponsorInterestEvent> Recent { get; set; } = new ();
    }
}
