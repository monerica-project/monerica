namespace DirectoryManager.Web.Services.Interfaces
{
    /// <summary>
    /// Quick "does this site exist?" check on a submission's main link, run before the preview
    /// so links that can never load (made-up domains, nothing listening) never reach review.
    /// </summary>
    public interface ISubmissionLinkChecker
    {
        /// <summary>
        /// Returns null when the link loads or the result is uncertain (benefit of the doubt),
        /// or a user-facing error when the link clearly cannot load.
        /// </summary>
        Task<string?> CheckAsync(string url, CancellationToken ct = default);
    }
}
