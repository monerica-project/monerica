namespace DirectoryManager.Data.Models.TransferModels
{
    public class WeeklyDirectoryEntries
    {
        required public string WeekStartDate { get; set; } // ISO 8601 formatted start date
        public List<DirectoryEntry> Entries { get; set; }
    }
}