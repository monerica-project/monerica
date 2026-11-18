using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Web.Models.Members;

namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Reviews and replies written by member accounts. One review per member per listing (saving
    /// again edits it). Every new or edited member review and every member reply is Pending until
    /// an admin approves it in the normal review moderation pages — never auto-published.
    /// </summary>
    public interface IMemberReviewService
    {
        Task<DirectoryEntryReview?> GetOwnReviewAsync(string userId, int directoryEntryId, CancellationToken ct = default);

        Task<(bool RequireOrderProof, bool RequireDetails)> GetVerificationRequirementsAsync(DirectoryEntry entry, CancellationToken ct = default);

        Task<MemberSaveResult> SaveReviewAsync(ApplicationUser member, DirectoryEntry? entry, MemberReviewInputModel input, CancellationToken ct = default);

        Task<MemberSaveResult> SaveReplyAsync(ApplicationUser member, int directoryEntryReviewId, MemberReplyInputModel input, CancellationToken ct = default);

        Task<MyMemberReviewsViewModel> ListOwnAsync(ApplicationUser member, CancellationToken ct = default);

        /// <summary>Deletes the member's own review; returns its listing id, or null when not theirs.</summary>
        Task<int?> DeleteOwnReviewAsync(string userId, int directoryEntryReviewId, CancellationToken ct = default);

        /// <summary>Deletes the member's own reply; returns its review id, or null when not theirs.</summary>
        Task<int?> DeleteOwnReplyAsync(string userId, int directoryEntryReviewCommentId, CancellationToken ct = default);
    }
}
