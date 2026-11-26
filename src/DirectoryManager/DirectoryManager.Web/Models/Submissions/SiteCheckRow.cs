namespace DirectoryManager.Web.Models.Submissions
{
    /// <summary>One weekly site-check finding, ready to show as a table row.</summary>
    public class SiteCheckRow
    {
        public DirectoryManager.Web.Helpers.SiteCheckFinding Finding { get; set; }

        public DirectoryManager.Data.Enums.DirectoryStatus? CurrentStatus { get; set; }

        /// <summary>The Tor address that failed (Tor findings only).</summary>
        public string? OnionLink { get; set; }

        /// <summary>Weekly checks in a row that failed (0 when not recorded).</summary>
        public int FailStreak { get; set; }

        public DateTime? LastCheckedUtc { get; set; }

        /// <summary>The text added to the listing's note on removal, e.g. "site offline (clearnet link offline)".</summary>
        public string? Reason { get; set; }

        /// <summary>The checker's warning text (warnings only).</summary>
        public string? Warning { get; set; }
    }
}
