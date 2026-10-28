using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISiteActivityRepository
    {
        /// <summary>
        /// Everything people submitted with a CreateDate in [fromUtc, toUtc): listing submissions
        /// (new and edits), reviews, review replies, problem reports and verification requests.
        /// Drafts (Preview) and the SiteChecker / DetailChecker's automated submissions are excluded.
        /// </summary>
        /// <returns>One item per submission, unsorted.</returns>
        Task<List<SiteActivityItem>> GetActivityAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    }
}
