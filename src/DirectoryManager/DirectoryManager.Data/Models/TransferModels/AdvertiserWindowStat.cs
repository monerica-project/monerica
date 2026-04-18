namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class AdvertiserWindowStat
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;
        public decimal RevenueInWindow { get; set; } // currency (USD)
        public int InvoiceCount { get; set; } // overlapping invoices
        public double OverlapDays { get; set; } // total overlapping days
    }
}