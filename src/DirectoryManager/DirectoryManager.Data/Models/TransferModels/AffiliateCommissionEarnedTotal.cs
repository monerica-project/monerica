namespace DirectoryManager.Data.Models.TransferModels
{
    public class AffiliateCommissionEarnedTotal
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;
        public decimal TotalUsdValue { get; set; }
        public int CommissionCount { get; set; }
        public DateTime? FirstCommissionDate { get; set; }
        public DateTime? LastCommissionDate { get; set; }
    }
}