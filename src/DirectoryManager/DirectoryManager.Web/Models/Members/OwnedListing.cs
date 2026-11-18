namespace DirectoryManager.Web.Models.Members
{
    public sealed class OwnedListing
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Key { get; set; }
    }
}
