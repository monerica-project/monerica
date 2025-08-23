// Web/Models/Affiliates/AffiliateCommissionLookupResultModel.cs
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Affiliates
{
    public class AffiliateCommissionLookupResultModel
    {
        public string ReferralCode { get; set; } = string.Empty;
        public string WalletAddress { get; set; } = string.Empty;
        public Currency PayoutCurrency { get; set; }
        public string? Email { get; set; }

        public List<AffiliateCommissionRow> Commissions { get; set; } = [];
    }
}
