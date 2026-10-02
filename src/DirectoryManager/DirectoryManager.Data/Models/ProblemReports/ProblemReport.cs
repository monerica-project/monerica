using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.ProblemReports
{
    // A public, captcha-gated report telling Monerica about a problem with a given listing
    // (wrong info, scam behavior, dead link, etc.). Mirrors VerificationRequest, but is
    // available on every listing regardless of status.
    public class ProblemReport : StateInfo
    {
        [Key]
        public int ProblemReportId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        // Required description of the problem the reporter is flagging.
        [Required]
        public string Comment { get; set; } = string.Empty;

        public ProblemReportStatus Status { get; set; } = ProblemReportStatus.Pending;

        // Abuse signal without storing raw IP: HMAC(IP) hex.
        [MaxLength(64)]
        public string? SourceIpHash { get; set; }

        // Unguessable public token so the reporter's optional "cover our review costs"
        // page lives at a unique GUID URL (/problem-reports/pay/{PaymentToken}).
        public Guid PaymentToken { get; set; } = Guid.NewGuid();

        // Set only if/when the reporter chooses to create a donation invoice on the shared
        // "Monerica - ReviewRequests" BTCPay store. Null = no invoice created.
        [MaxLength(100)]
        public string? BtcPayInvoiceId { get; set; }

        // Populated once a payment is detected on the invoice (poll-on-admin-view).
        public DateTime? PaidUtc { get; set; }

        public decimal? PaidAmount { get; set; }

        [MaxLength(10)]
        public string? PaidCurrency { get; set; }
    }
}
