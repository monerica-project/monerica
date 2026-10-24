using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    /// <summary>A listing shown in a sponsor spot on /sponsors-and-featured.</summary>
    public class SpotListing
    {
        public string Name { get; set; } = string.Empty;

        public string ProfilePath { get; set; } = string.Empty;

        public DirectoryStatus DirectoryStatus { get; set; }
    }
}
