namespace DirectoryManager.Data.Models
{
    public class GroupedDirectoryEntry
    {
        required public string Date { get; set; }
        required public IEnumerable<DirectoryEntry> Entries { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}