using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IMarketingEmailContactRepository
    {
        Task<MarketingEmailContact?> GetByTokenAsync(string token);

        Task<MarketingEmailContact?> GetByEmailAsync(string email);

        /// <summary>Returns the contact for this address, creating it (with a new token) if needed.</summary>
        Task<MarketingEmailContact> GetOrCreateAsync(string email, string? note = null);

        /// <summary>Opts a contact out (or back in). Returns false if the contact does not exist.</summary>
        Task<bool> SetOptedOutAsync(int marketingEmailContactId, bool optedOut, string source);

        Task<int> CountAsync(bool optedOutOnly);

        Task<List<MarketingEmailContact>> ListPageAsync(int page, int pageSize, bool optedOutOnly);
    }
}
