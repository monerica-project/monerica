using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// Per-directory-entry health streaks used by the SiteChecker job. A listing is
    /// flagged offline only after it has come back unreachable across MULTIPLE
    /// consecutive runs — a single flaky/slow run (routine for Tor onions, which
    /// time out or fail their SOCKS circuit even when alive) is never enough on its
    /// own. Clearnet and onion links are tracked independently so a dead onion is not
    /// masked by a live clearnet link. One row per DirectoryEntry.
    /// </summary>
    public class SiteCheckStatus : StateInfo
    {
        public int SiteCheckStatusId { get; set; }

        public int DirectoryEntryId { get; set; }

        /// <summary>
        /// Consecutive runs the clearnet link(s) came back NOT online. Reset to 0 on
        /// any online result. (Clearnet verdicts are already definitive — this is kept
        /// for the audit trail; clearnet still flags on the first confirmed offline.)
        /// </summary>
        public int ClearnetFailStreak { get; set; }

        /// <summary>
        /// Consecutive runs the onion link came back NOT online (inconclusive timeout /
        /// SOCKS failure, or a definitive gone response). Reset to 0 on any online
        /// result. This is the streak the flag threshold is applied to.
        /// </summary>
        public int OnionFailStreak { get; set; }

        public DateTime LastCheckedUtc { get; set; }
    }
}
