using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// The weekly SiteChecker queues "automated" submissions (site offline, SSL problem, dead
    /// additional-subcategory link) that carry a full copy of the listing taken at check time.
    /// If an admin edits the listing before reviewing the submission, approving that stale copy
    /// would silently undo the edits. The review page therefore refreshes the copy from the live
    /// listing; this class decides which submissions qualify and what status/note to propose.
    /// </summary>
    /// <summary>What a weekly SiteChecker submission is about, as shown on the review page.</summary>
    public enum SiteCheckFinding
    {
        /// <summary>Not a SiteChecker submission.</summary>
        None = 0,

        /// <summary>The main (clearnet) site did not load. Proposal: remove the listing as offline.</summary>
        MainSiteOffline = 1,

        /// <summary>Only the Tor address did not load; the main site did.</summary>
        TorLinkOffline = 2,

        /// <summary>A warning that proposes no change (bad certificate, dead additional-subcategory link).</summary>
        Warning = 3,
    }

    public static class AutomatedSubmissionRefresh
    {
        /// <summary>NoteToAdmin prefix written by the SiteChecker (and only by it).</summary>
        public const string SiteCheckerMarker = "(automated submission)";

        /// <summary>The reason text an offline submission appends to the listing's note.</summary>
        public const string SiteOfflineMessage = "site offline";

        /// <summary>
        /// True for a still-pending SiteChecker submission about an existing listing. The
        /// DetailChecker's submissions ("[auto-detail-check …") are deliberately excluded:
        /// they propose real field changes that a refresh would wipe out.
        /// </summary>
        /// <returns>Whether the submission's listing copy should be refreshed.</returns>
        public static bool ShouldRefresh(bool isPending, int? directoryEntryId, string? noteToAdmin)
            => isPending
               && directoryEntryId is > 0
               && noteToAdmin is not null
               && noteToAdmin.StartsWith(SiteCheckerMarker, StringComparison.Ordinal);

        /// <summary>
        /// The status and public note to propose against the CURRENT listing.
        /// An offline submission keeps proposing "Removed" with its "site offline (…)" reason added
        /// to the current note; every other SiteChecker submission proposes no change, so it takes
        /// the listing's current status and note.
        /// </summary>
        /// <returns>The status and note to show on the review form.</returns>
        public static (DirectoryStatus Status, string? Note) ProposedStatusAndNote(
            DirectoryStatus proposedStatus,
            string? proposedNote,
            DirectoryStatus currentStatus,
            string? currentNote)
        {
            var reason = OfflineReason(proposedStatus, proposedNote);
            if (reason is null)
            {
                return (currentStatus, currentNote);
            }

            if (string.IsNullOrWhiteSpace(currentNote))
            {
                return (DirectoryStatus.Removed, reason);
            }

            return currentNote.Contains(reason, StringComparison.OrdinalIgnoreCase)
                ? (DirectoryStatus.Removed, currentNote)
                : (DirectoryStatus.Removed, $"{currentNote} | {reason}");
        }

        /// <summary>Sorts a submission into what the SiteChecker found, from its notes alone.</summary>
        /// <returns>The finding, or <see cref="SiteCheckFinding.None"/> for anything a person or another job sent.</returns>
        public static SiteCheckFinding Classify(string? noteToAdmin, DirectoryStatus? proposedStatus, string? proposedNote)
        {
            if (noteToAdmin is null || !noteToAdmin.StartsWith(SiteCheckerMarker, StringComparison.Ordinal))
            {
                return SiteCheckFinding.None;
            }

            var reason = OfflineReason(proposedStatus ?? DirectoryStatus.Unknown, proposedNote);
            if (reason is null)
            {
                return SiteCheckFinding.Warning;
            }

            // "site offline (tor link offline)" is the only reason where the main site was up.
            return reason.Contains("clearnet", StringComparison.OrdinalIgnoreCase)
                   || !reason.Contains("tor link", StringComparison.OrdinalIgnoreCase)
                ? SiteCheckFinding.MainSiteOffline
                : SiteCheckFinding.TorLinkOffline;
        }

        /// <summary>The checker's warning text without its "(automated submission)" marker.</summary>
        /// <returns>The warning, or null when there is none.</returns>
        public static string? WarningText(string? noteToAdmin)
        {
            if (noteToAdmin is null || !noteToAdmin.StartsWith(SiteCheckerMarker, StringComparison.Ordinal))
            {
                return null;
            }

            var text = noteToAdmin[SiteCheckerMarker.Length..].Trim();
            return text.Length == 0 ? null : text;
        }

        // "site offline (clearnet link offline)" etc. — from the last occurrence to the end, since
        // the checker builds the note as "<listing note> | <reason>".
        public static string? OfflineReason(DirectoryStatus proposedStatus, string? proposedNote)
        {
            if (proposedStatus != DirectoryStatus.Removed || string.IsNullOrEmpty(proposedNote))
            {
                return null;
            }

            var idx = proposedNote.LastIndexOf(SiteOfflineMessage, StringComparison.OrdinalIgnoreCase);
            return idx < 0 ? null : proposedNote[idx..].Trim();
        }
    }
}
