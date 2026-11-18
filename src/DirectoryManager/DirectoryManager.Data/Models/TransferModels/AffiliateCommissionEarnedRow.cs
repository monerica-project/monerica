namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>One affiliate commission, reduced to what the by-month report needs.</summary>
    public class AffiliateCommissionEarnedRow
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = string.Empty;
        public DateTime CommissionDate { get; set; }
        public decimal UsdValue { get; set; }
    }
}
