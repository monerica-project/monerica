using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// What a listing's own pages said about KYC, countries, VPN/Tor and fees the last time the
    /// monthly terms watcher read them (DirectoryManager.TermsWatcher). One row per listing. The
    /// watcher never edits a listing: when the wording changes it records the difference here and,
    /// if needed, marks the row for the owner to review on /admin/terms-watch.
    /// </summary>
    [Index(nameof(DirectoryEntryId), IsUnique = true)]
    [Index(nameof(NeedsReview))]
    public class ListingTermsSnapshot
    {
        public int Id { get; set; }

        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        public DateTime LastCheckedUtc { get; set; }

        /// <summary>Last time the pages could actually be read.</summary>
        public DateTime? LastReadUtc { get; set; }

        /// <summary>Pages that were read (one URL per line).</summary>
        public string? Pages { get; set; }

        /// <summary>The policy sentences found on those pages, one per line (the comparison basis).</summary>
        public string? Sentences { get; set; }

        /// <summary>SHA-256 of the normalized sentences: equal fingerprint = nothing changed.</summary>
        [MaxLength(64)]
        public string? Fingerprint { get; set; }

        public TermsWatchResult Result { get; set; }

        /// <summary>When the wording last changed.</summary>
        public DateTime? ChangedUtc { get; set; }

        /// <summary>Sentences added (+) and removed (-) at the last change.</summary>
        public string? Diff { get; set; }

        /// <summary>The AI's plain explanation of what the change means for the listing.</summary>
        [MaxLength(2000)]
        public string? Summary { get; set; }

        /// <summary>The AI's suggested corrections (field: value lines). Never applied automatically.</summary>
        [MaxLength(2000)]
        public string? Suggestion { get; set; }

        /// <summary>True until the owner presses "Handled" on /admin/terms-watch.</summary>
        public bool NeedsReview { get; set; }

        public DateTime? HandledUtc { get; set; }

        /// <summary>Checks in a row where the site could not be read.</summary>
        public int ReadFailures { get; set; }
    }
}
