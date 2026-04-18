using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models.Affiliates
{
    [Index(nameof(ReferralCode), IsUnique = true)]
    public class AffiliateAccount : StateInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AffiliateAccountId { get; set; }

        // Code the affiliate chose (what buyers use). Keep it short & indexable.
        [Required]
        [StringLength(12, MinimumLength = 3)]
        public string ReferralCode { get; set; } = string.Empty;

        // Where payouts will be sent (crypto address etc.)
        [Required]
        [MaxLength(256)]
        public string WalletAddress { get; set; } = string.Empty;

        // Currency to pay this affiliate in (uses your existing enum)
        [Required]
        public Currency PayoutCurrency { get; set; } = Currency.Unknown;

        // Optional email for comms
        [MaxLength(256)]
        public string? Email { get; set; }

        // Nav: commissions created for this affiliate
        public virtual ICollection<AffiliateCommission> Commissions { get; set; } = new List<AffiliateCommission>();
    }
}