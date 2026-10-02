namespace DirectoryManager.Web.Models.ProblemReports
{
    // Backs the optional "cover our review costs" page at /problem-reports/pay/{token}.
    public class ProblemReportPayViewModel
    {
        public Guid Token { get; set; }

        public string EntryName { get; set; } = string.Empty;

        // Show the swap-fees note only for exchange/swap listings.
        public bool IsExchange { get; set; }

        // A payment was already detected for this report.
        public bool AlreadyPaid { get; set; }
    }
}
