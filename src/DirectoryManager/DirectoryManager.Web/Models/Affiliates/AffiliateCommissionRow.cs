using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Affiliates
{
    public class AffiliateCommissionRow
    {
        public int AffiliateCommissionId { get; set; }
        public int SponsoredListingInvoiceId { get; set; }
        public decimal AmountDue { get; set; }
        public Currency PayoutCurrency { get; set; }
        public CommissionPayoutStatus PayoutStatus { get; set; }
        public string? PayoutTransactionId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }
    }
}