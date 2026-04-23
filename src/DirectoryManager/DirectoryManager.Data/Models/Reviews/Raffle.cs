using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    /// <summary>
    /// A named raffle window. Entries (DirectoryEntryReviewRaffleEntry) are associated
    /// to a single Raffle. Only one enabled raffle should be "active" at a time
    /// (StartDate &lt;= now &lt;= EndDate), but that is enforced by convention, not schema.
    /// </summary>
    public class Raffle : UserStateInfo
    {
        [Key]
        public int RaffleId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Master on/off switch. A disabled raffle will never be treated as "active"
        /// regardless of dates.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        public ICollection<DirectoryEntryReviewRaffleEntry> Entries { get; set; }
            = new List<DirectoryEntryReviewRaffleEntry>();

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
