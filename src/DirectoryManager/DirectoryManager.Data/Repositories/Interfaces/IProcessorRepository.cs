using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IProcessorRepository
    {
        Task<Processor?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Processor?> GetByNameAsync(string name, CancellationToken ct = default);

        Task<List<Processor>> ListAllAsync(CancellationToken ct = default);

        Task<Processor> CreateAsync(Processor entity, CancellationToken ct = default);
        Task UpdateAsync(Processor entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);

        // ✅ for dropdowns later
        Task<List<IdNameOption>> ListOptionsAsync(CancellationToken ct = default);
    }
}