using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IVerificationRequestRepository
    {
        Task AddAsync(VerificationRequest entity, CancellationToken ct = default);

        Task<VerificationRequest?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<List<VerificationRequest>> ListByStatusAsync(VerificationRequestStatus status, int page, int pageSize, CancellationToken ct = default);

        Task<int> CountByStatusAsync(VerificationRequestStatus status, CancellationToken ct = default);

        Task SetStatusAsync(int id, VerificationRequestStatus status, CancellationToken ct = default);
    }
}
