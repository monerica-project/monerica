using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// Page views of a listing's /site/{key} page (monerica.com + app.monerica.com), copied nightly
    /// from the self-hosted analytics database by a server job. Counts only, no visitor data.
    /// The analytics keeps each day's busiest pages, so quiet listings may show 0.
    /// </summary>
    public class ListingPageView
    {
        [Key]
        [MaxLength(255)]
        public string DirectoryEntryKey { get; set; } = string.Empty;

        public int Views30 { get; set; }

        public int Views90 { get; set; }

        public DateTime UpdatedUtc { get; set; }
    }
}
