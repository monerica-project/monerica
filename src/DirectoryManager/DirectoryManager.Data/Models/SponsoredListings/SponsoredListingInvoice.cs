using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingInvoice : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SponsoredListingInvoiceId { get; set; }

        /// <summary>
        /// Used as a reference to the invoice in the payment processor (e.g. order id).
        /// </summary>
        public Guid InvoiceId { get; set; }

        public string InvoiceDescription { get; set; } = string.Empty;

        public int DirectoryEntryId { get; set; }

        public DateTime CampaignStartDate { get; set; }

        public DateTime CampaignEndDate { get; set; }

        /// <summary>
        /// Amount user paid.
        /// </summary>
        public decimal PaidAmount { get; set; }

        /// <summary>
        /// Amount received after payment.
        /// </summary>
        public decimal OutcomeAmount { get; set; }

        public Currency PaidInCurrency { get; set; } = Currency.Unknown;

        public decimal Amount { get; set; }

        public Currency Currency { get; set; } = Currency.Unknown;

        public PaymentProcessor PaymentProcessor { get; set; }

        [MaxLength(255)]
        public string ProcessorInvoiceId { get; set; } = string.Empty;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unknown;

        /// <summary>
        /// The request sent to the payment processor when the invoice was created.
        /// </summary>
        public string InvoiceRequest { get; set; } = string.Empty;

        /// <summary>
        /// The response from the payment processor when the invoice was created.
        /// </summary>
        public string InvoiceResponse { get; set; } = string.Empty;

        /// <summary>
        /// The response from the payment processor when the payment was made.
        /// </summary>
        public string PaymentResponse { get; set; } = string.Empty;

        public int? SponsoredListingId { get; set; }

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;

        public int? SubCategoryId { get; set; }

        [MaxLength(255)]
        public string? IpAddress { get; set; }

        public Guid ReservationGuid { get; set; }

        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        public bool IsReminderSent { get; set; }

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public virtual SponsoredListing? SponsoredListing { get; set; }
    }
}