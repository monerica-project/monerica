namespace DirectoryManager.Data.Models
{
    public class GroupedDirectoryEntry
    {
        public required string Date { get; set; }
        public string Name { get; set; } = string.Empty;
        public required IEnumerable<DirectoryEntry> Entries { get; set; }
    }
}