using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class AffiliateCommissionEarned : UserStateInfo, IEquatable<AffiliateCommissionEarned>
    {
        [Key]
        public int AffiliateCommissionEarnedId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        /// <summary>
        /// The date the commission was received / recorded.
        /// </summary>
        [Required]
        [Column(TypeName = "datetime2")]
        public DateTime CommissionDate { get; set; }

        /// <summary>
        /// USD value at the time of the commission.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UsdValue { get; set; }

        /// <summary>
        /// The currency the payment was actually received in (e.g. XMR, BTC, USD).
        /// </summary>
        [Required]
        public Currency PaymentCurrency { get; set; }

        /// <summary>
        /// Amount in the payment currency.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,8)")]
        public decimal PaymentCurrencyAmount { get; set; }

        [MaxLength(255)]
        public string? TransactionId { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        public bool Equals(AffiliateCommissionEarned? other)
        {
            if (other == null)
            {
                return false;
            }

            return this.AffiliateCommissionEarnedId == other.AffiliateCommissionEarnedId &&
                   this.DirectoryEntryId == other.DirectoryEntryId &&
                   this.CommissionDate == other.CommissionDate &&
                   this.UsdValue == other.UsdValue &&
                   this.PaymentCurrency == other.PaymentCurrency &&
                   this.PaymentCurrencyAmount == other.PaymentCurrencyAmount &&
                   this.TransactionId == other.TransactionId &&
                   this.Note == other.Note;
        }

        public override int GetHashCode()
        {
            return this.AffiliateCommissionEarnedId.GetHashCode();
        }
    }
}