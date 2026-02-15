using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
{
    public class SponsorTickerItemVm
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryKey { get; set; } = "";
        public string Link { get; set; }
        public string Name { get; set; } = "";
        public SponsorshipType Tier { get; set; }
    }
}
