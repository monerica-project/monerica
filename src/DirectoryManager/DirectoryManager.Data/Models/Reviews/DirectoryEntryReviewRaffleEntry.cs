using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    /// <summary>
    /// Stores a crypto payout address for a raffle tied to a single review.
    /// One entry per review (enforced via unique index on DirectoryEntryReviewId).
    /// </summary>
    public class DirectoryEntryReviewRaffleEntry : UserStateInfo
    {
        [Key]
        public int DirectoryEntryReviewRaffleEntryId { get; set; }

        // FK → DirectoryEntryReview (1-to-1 enforced at DB level via unique index)
        [Required]
        public int DirectoryEntryReviewId { get; set; }

        public DirectoryEntryReview DirectoryEntryReview { get; set; } = null!;

        // Crypto type identifier, e.g. "XMR", "BTC", "ETH"
        [Required]
        [MaxLength(20)]
        public string CryptoType { get; set; } = string.Empty;

        // The recipient wallet address for the raffle payout
        [Required]
        [MaxLength(512)]
        public string CryptoAddress { get; set; } = string.Empty;

        // Lifecycle status for this raffle entry
        public RaffleEntryStatus Status { get; set; } = RaffleEntryStatus.Pending;

        // Optional: txid or reference once payment is sent
        [MaxLength(256)]
        public string? PaymentReference { get; set; }

        // Concurrency
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
