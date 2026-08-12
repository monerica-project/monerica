namespace DirectoryManager.Web.Models.Submissions
{
    // Backs the no-JS Monero checkout at /submission/pay/{token}/invoice.
    public class SubmissionInvoiceViewModel
    {
        public Guid Token { get; set; }

        public string Name { get; set; } = string.Empty;

        // Monero address for this submission's donation invoice.
        public string Address { get; set; } = string.Empty;

        // Inline base64 PNG QR of monero:{address} (no external request needed).
        public string QrDataUri { get; set; } = string.Empty;

        // A payment has been detected.
        public bool Paid { get; set; }
    }
}
