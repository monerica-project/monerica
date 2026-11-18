using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    /// <summary>Stored email content, one row per distinct message (see <see cref="EmailSendLogContent"/>).</summary>
    public interface IEmailSendLogContentRepository
    {
        /// <summary>
        /// Returns the id of the row whose <see cref="EmailSendLogContent.ContentHash"/> matches
        /// <paramref name="candidate"/> (moving its LastUsedUtc forward), or inserts the candidate.
        /// </summary>
        /// <returns>The content row id.</returns>
        int GetOrCreate(EmailSendLogContent candidate);

        EmailSendLogContent? Get(int emailSendLogContentId);

        /// <summary>Deletes content not used since <paramref name="olderThanUtc"/>; log rows pointing
        /// at it keep existing with no content.</summary>
        /// <returns>Number of content rows deleted.</returns>
        Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken ct = default);
    }
}
