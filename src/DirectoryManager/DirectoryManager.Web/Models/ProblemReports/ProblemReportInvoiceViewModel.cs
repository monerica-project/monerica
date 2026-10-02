namespace DirectoryManager.Web.Models.ProblemReports
{
    // Backs the no-JS Monero checkout at /problem-reports/pay/{token}/invoice.
    public class ProblemReportInvoiceViewModel
    {
        public Guid Token { get; set; }

        public string EntryName { get; set; } = string.Empty;

        // Monero address for this report's donation invoice.
        public string Address { get; set; } = string.Empty;

        // Inline base64 PNG QR of monero:{address} (no external request needed).
        public string QrDataUri { get; set; } = string.Empty;

        // A payment has been detected.
        public bool Paid { get; set; }
    }
}
