namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class AdvertiserWindowSum
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;

        // Sum of Amount (USD) for PAID invoices created in the window
        public decimal Revenue { get; set; }

        // Count of PAID invoices created in the window
        public int Count { get; set; }

        // Sum of purchased campaign days across those invoices
        public int DaysPurchased { get; set; }
    }
}
