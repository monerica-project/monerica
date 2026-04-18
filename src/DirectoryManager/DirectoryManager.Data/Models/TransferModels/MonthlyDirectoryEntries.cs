namespace DirectoryManager.Data.Models.TransferModels
{
    public class MonthlyDirectoryEntries
    {
        public string MonthKey { get; set; } = string.Empty; // ISO 8601 formatted month
        public IEnumerable<DirectoryEntry> Entries { get; set; } = new List<DirectoryEntry>();
    }
}