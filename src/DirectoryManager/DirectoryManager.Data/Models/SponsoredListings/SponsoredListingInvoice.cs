using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingInvoice : StateInfo
    {
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
        /// todo: needs to be to 12 decimal places.
        /// </summary>
        public decimal PaidAmount { get; set; }

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

        public virtual DirectoryEntry? DirectoryEntry { get; set; }

        public virtual SponsoredListing? SponsoredListing { get; set; }
    }
}