// Web/Models/Affiliates/AffiliateSignupInputModel.cs
using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Affiliates
{
    public class AffiliateSignupInputModel
    {
        [Required]
        [StringLength(12, MinimumLength = 3)]
        public string ReferralCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string WalletAddress { get; set; } = string.Empty;

        [Required]
        public Currency PayoutCurrency { get; set; }

        [EmailAddress]
        [MaxLength(256)]
        public string? Email { get; set; }
        public string? Captcha { get; set; }
    }
}
