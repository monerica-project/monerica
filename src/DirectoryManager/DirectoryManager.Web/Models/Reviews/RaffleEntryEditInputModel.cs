using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.Reviews
{
    /// <summary>
    /// Admin edit model for a single <see cref="DirectoryManager.Data.Models.Reviews.DirectoryEntryReviewRaffleEntry"/>.
    /// Used to update the entry's lifecycle status (e.g. Pending → Paid) and to record
    /// the payment reference (txid) once a payout has been sent.
    /// </summary>
    public class RaffleEntryEditInputModel
    {
        public int DirectoryEntryReviewRaffleEntryId { get; set; }

        public int RaffleId { get; set; }

        // Read-only context (shown on the form, not editable).
        public int DirectoryEntryReviewId { get; set; }

        public DateTime CreateDate { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Crypto type")]
        public string CryptoType { get; set; } = string.Empty;

        [Required]
        [MaxLength(512)]
        [Display(Name = "Payout address")]
        public string CryptoAddress { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public RaffleEntryStatus Status { get; set; } = RaffleEntryStatus.Pending;

        [MaxLength(256)]
        [Display(Name = "Payment reference (txid)")]
        public string? PaymentReference { get; set; }
    }
}
