using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.VideoSponsorships
{
    // The invoice for a video sponsorship purchase. Deliberately separate from
    // SponsoredListingInvoice: site-sponsor reporting, reminders and slot logic all assume
    // every row there is a dated site placement.
    public class VideoSponsorOrder : StateInfo
    {
        [Key]
        public int VideoSponsorOrderId { get; set; }

        // Public, unguessable reference used in URLs and as the processor order id.
        public Guid InvoiceId { get; set; } = Guid.NewGuid();

        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        public int VideoSponsorOfferId { get; set; }

        public VideoSponsorOffer VideoSponsorOffer { get; set; } = null!;

        // Snapshots taken at purchase so later price/name edits never rewrite history.
        [MaxLength(150)]
        public string OfferName { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        // UnitPrice x Quantity, before any Existing Sponsor Perk.
        public decimal OriginalAmount { get; set; }

        public decimal DiscountPercent { get; set; }

        public decimal DiscountAmount { get; set; }

        // The site-sponsor tier that granted the perk (null = no discount).
        public SponsorshipType? DiscountBasisTier { get; set; }

        // What the buyer said demonstrating their product costs (buying it, doing a swap, ...).
        // Only collected when it is more than $100; charged in full, never discounted. 0 = none.
        public decimal DemoCostUsd { get; set; }

        // The amount charged (USD): the discounted sponsorship price plus DemoCostUsd.
        public decimal Amount { get; set; }

        public Currency Currency { get; set; } = Currency.USD;

        // What actually arrived (XMR) and the rate BTCPay locked.
        public decimal PaidAmount { get; set; }

        public Currency PaidInCurrency { get; set; } = Currency.Unknown;

        public decimal PaymentRate { get; set; }

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.InvoiceCreated;

        public DateTime? PaidUtc { get; set; }

        [MaxLength(255)]
        public string ProcessorInvoiceId { get; set; } = string.Empty;

        public DateTime? InvoiceExpiresUtc { get; set; }

        [MaxLength(500)]
        public string InvoiceDescription { get; set; } = string.Empty;

        public string InvoiceRequest { get; set; } = string.Empty;

        public string InvoiceResponse { get; set; } = string.Empty;

        public string PaymentResponse { get; set; } = string.Empty;

        // Optional contact for the "your video is live" email.
        [MaxLength(100)]
        public string? Email { get; set; }

        // What the buyer wants in the video.
        [MaxLength(4000)]
        public string? Notes { get; set; }

        // Monero address for a manual refund (minus network fees) if the video can't be delivered.
        [Required]
        [MaxLength(120)]
        public string RefundAddress { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? IpAddress { get; set; }

        // Set when the order was paid but one of its videos had meanwhile been sold to someone else.
        public bool HasConflict { get; set; }

        [MaxLength(2000)]
        public string? AdminNote { get; set; }

        public List<VideoSponsorOrderItem> Items { get; set; } = new ();
    }
}
