using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IRevenueRepository
    {
        // Every paid video sponsorship, affiliate commission and donation, in USD.
        Task<List<RevenueItem>> GetOneTimeRevenueAsync(CancellationToken ct = default);
    }
}
