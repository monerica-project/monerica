using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// A deposit guarantee for a listing: a proof link and a whole-USD amount. A listing can have
    /// several (capped by IntegerConstants.MaxGuarantees). Managed with the same delete-all /
    /// recreate sync pattern as <see cref="AdditionalLink"/>.
    /// </summary>
    public sealed class DirectoryEntryGuarantee : UserStateInfo
    {
        [Key]
        public int DirectoryEntryGuaranteeId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        // Keeps display ordering stable (1..MaxGuarantees).
        [Range(1, 4)]
        public int SortOrder { get; set; } = 1;

        [Required]
        [Url]
        [MaxLength(500)]
        public string Link { get; set; } = string.Empty;

        /// <summary>The deposit-guarantee amount, denominated in <see cref="Currency"/> (e.g. 30000 USD, 1 BTC).</summary>
        public decimal Amount { get; set; }

        /// <summary>Currency of <see cref="Amount"/> — USD, BTC or XMR. Defaults to USD.</summary>
        public Currency Currency { get; set; } = Currency.USD;

        [ForeignKey(nameof(DirectoryEntryId))]
        public DirectoryEntry? DirectoryEntry { get; set; }
    }
}
