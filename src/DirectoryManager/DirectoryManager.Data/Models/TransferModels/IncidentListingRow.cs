using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>The current, public facts of a listing named on the incidents page.</summary>
    public sealed class IncidentListingRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public DirectoryStatus Status { get; set; }

        public string? Note { get; set; }

        /// <summary>The listing's main website (null/blank = none).</summary>
        public string? Link { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime? UpdateDate { get; set; }
    }
}
