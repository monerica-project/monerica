using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubmissionBlockedTermRepository
    {
        Task<IEnumerable<SubmissionBlockedTerm>> GetAllAsync();

        /// <summary>Enabled terms only — what submission validation is checked against.</summary>
        Task<IReadOnlyList<SubmissionBlockedTerm>> GetEnabledAsync();

        Task<SubmissionBlockedTerm?> CreateAsync(SubmissionBlockedTerm model);

        Task<bool> ToggleEnabledAsync(int id);

        Task DeleteAsync(int id);
    }
}
