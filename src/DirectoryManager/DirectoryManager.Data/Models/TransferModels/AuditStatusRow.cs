using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>One audit snapshot, reduced to what the public incidents page needs.</summary>
    public sealed class AuditStatusRow
    {
        public int AuditId { get; set; }

        public int DirectoryEntryId { get; set; }

        public DirectoryStatus Status { get; set; }

        /// <summary>When the snapshot's state took effect (UpdateDate, else CreateDate).</summary>
        public DateTime EffectiveUtc { get; set; }

        /// <summary>The listing's public note at that time.</summary>
        public string? Note { get; set; }
    }
}
