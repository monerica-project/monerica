namespace DirectoryManager.Web.Models.Revenue
{
    /// <summary>A listing + placement where someone started paying but never finished.</summary>
    public class AbandonedCheckoutRow
    {
        public int LatestInvoiceId { get; set; }

        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ProfilePath { get; set; }

        public string Placement { get; set; } = string.Empty;

        public string? Email { get; set; }

        public decimal Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public DateTime LastAttempt { get; set; }

        public string Status { get; set; } = string.Empty;

        public int WaitlistCount { get; set; }
    }
}
