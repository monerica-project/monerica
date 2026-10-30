namespace DirectoryManager.Web.Models.VerificationRequests
{
    public class VerificationRequestsByListingAgeViewModel
    {
        public List<VerificationRequestAgeRow> Rows { get; set; } = new ();

        public int MinimumDaysListed { get; set; }
    }
}
