// Web/Models/Affiliates/AffiliateCommissionLookupInputModel.cs
using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Affiliates
{
    public class AffiliateCommissionLookupInputModel
    {
        [Required]
        public string ReferralCode { get; set; } = string.Empty;

        [Required, MaxLength(256)]
        public string WalletAddress { get; set; } = string.Empty;
    }
}
