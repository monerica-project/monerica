using DirectoryManager.Data.Models;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISubmissionBlockedDomainRepository
    {
        Task<IEnumerable<SubmissionBlockedDomain>> GetAllAsync();

        /// <summary>Enabled domains only — what submission links are checked against.</summary>
        Task<IReadOnlyList<SubmissionBlockedDomain>> GetEnabledAsync();

        /// <summary>Adds the domain (normalized); null when it is blank, invalid or already listed.</summary>
        Task<SubmissionBlockedDomain?> CreateAsync(SubmissionBlockedDomain model);

        Task<bool> ToggleEnabledAsync(int id);

        Task DeleteAsync(int id);
    }
}
