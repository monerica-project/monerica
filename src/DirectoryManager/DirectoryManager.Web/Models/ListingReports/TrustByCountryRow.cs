namespace DirectoryManager.Web.Models.ListingReports
{
    public class TrustByCountryRow
    {
        public string Country { get; set; } = string.Empty;

        public int Verified { get; set; }

        public int Admitted { get; set; }

        public int Questionable { get; set; }

        public int Scam { get; set; }

        public int Total => this.Verified + this.Admitted + this.Questionable + this.Scam;

        public double ScamPercent => this.Total == 0 ? 0 : this.Scam * 100.0 / this.Total;

        public double VerifiedPercent => this.Total == 0 ? 0 : this.Verified * 100.0 / this.Total;
    }
}
