using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Models
{
    public class DirectoryEntryWrapper
    {
        required public DirectoryEntry DirectoryEntry { get; set; }

        public bool IsSponsored { get; set; }

        public string GetLink()
        {
            // If sponsored, always return the regular link, not the affiliate link.
            return this.IsSponsored || string.IsNullOrWhiteSpace(this.DirectoryEntry.LinkA)
                ? this.DirectoryEntry.Link
                : this.DirectoryEntry.LinkA;
        }

        public string GetName()
        {
            return this.DirectoryEntry.Name;
        }
    }
}