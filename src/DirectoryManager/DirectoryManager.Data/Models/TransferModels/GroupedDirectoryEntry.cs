using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Models
{
    public class GroupedDirectoryEntry
    {
        public string Date { get; set; } // Use the appropriate type for the date
        public string Name { get; set; }
        public IEnumerable<DirectoryEntry> Entries { get; set; }
        public object Link { get; internal set; }
    }

}
