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

        // "site offline (clearnet link offline)" etc. — from the last occurrence to the end, since
        // the checker builds the note as "<listing note> | <reason>".
        private static string? OfflineReason(DirectoryStatus proposedStatus, string? proposedNote)
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
