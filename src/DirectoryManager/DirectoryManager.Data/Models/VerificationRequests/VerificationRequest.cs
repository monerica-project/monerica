using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.VerificationRequests
{
    // A public, captcha-gated request asking Monerica to verify a given listing.
    public class VerificationRequest : StateInfo
    {
        [Key]
        public int VerificationRequestId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        // Required reason the requester gives for wanting this listing verified.
        [Required]
        public string Comment { get; set; } = string.Empty;

        public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;

        // Abuse signal without storing raw IP: HMAC(IP) hex.
        [MaxLength(64)]
        public string? SourceIpHash { get; set; }

        // Unguessable public token so the requester's optional "cover our review costs"
        // page lives at a unique GUID URL (/verification-requests/pay/{PaymentToken}).
        public Guid PaymentToken { get; set; } = Guid.NewGuid();

        // Set only if/when the requester chooses to create a donation invoice on the
        // dedicated "Monerica - ReviewRequests" BTCPay store. Null = no invoice created.
        [MaxLength(100)]
        public string? BtcPayInvoiceId { get; set; }

        // Populated once a payment is detected on the invoice (poll-on-admin-view).
        public DateTime? PaidUtc { get; set; }

        public decimal? PaidAmount { get; set; }

        [MaxLength(10)]
        public string? PaidCurrency { get; set; }
    }
}
