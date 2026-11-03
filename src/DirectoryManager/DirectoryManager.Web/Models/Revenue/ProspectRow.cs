namespace DirectoryManager.Web.Models.Revenue
{
    /// <summary>A popular listing that could sponsor but never has.</summary>
    public class ProspectRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ProfilePath { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public int Views30 { get; set; }

        public int Views90 { get; set; }

        public int InterestLooks { get; set; }

        public string? Email { get; set; }
    }
}
