using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public sealed class DirectoryEntrySitemapRow
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryKey { get; set; } = string.Empty;

        public DirectoryStatus DirectoryStatus { get; set; }

        public DateTime CreateDate { get; set; }
        public DateTime? UpdateDate { get; set; }

        public string? CountryCode { get; set; }
    }
}