using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Models
{
    public class TaggedEntriesViewModel
    {
        public Tag Tag { get; set; } = null!;
        public IReadOnlyList<DirectoryEntryViewModel> Entries { get; set; } = new List<DirectoryEntryViewModel>();
    }
}
