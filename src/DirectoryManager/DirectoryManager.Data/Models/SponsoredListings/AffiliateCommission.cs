using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using DirectoryManager.Data.Models.SponsoredListings;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models.Affiliates
{
    // One commission per invoice (adjust IsUnique=false if you ever want multi-tier)
    [Index(nameof(SponsoredListingInvoiceId), IsUnique = true)]
    public class AffiliateCommission : StateInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AffiliateCommissionId { get; set; }

        [Required]
        public int SponsoredListingInvoiceId { get; set; }

        [Required]
        public int AffiliateAccountId { get; set; }

        // Amount due to affiliate (choose precision appropriate for your payouts)
        [Column(TypeName = "decimal(18,8)")]
        public decimal AmountDue { get; set; }

        // Currency to pay this commission in (usually matches the affiliate's PayoutCurrency)
        [Required]
        public Currency PayoutCurrency { get; set; } = Currency.Unknown;

        public CommissionPayoutStatus PayoutStatus { get; set; } = CommissionPayoutStatus.Unknown;

        [MaxLength(255)]
        public string? PayoutTransactionId { get; set; }

        // Navs
        public virtual SponsoredListingInvoice SponsoredListingInvoice { get; set; } = null!;
        public virtual AffiliateAccount AffiliateAccount { get; set; } = null!;
    }
}
