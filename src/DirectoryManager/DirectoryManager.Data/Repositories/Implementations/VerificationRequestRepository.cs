using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.VerificationRequests;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class VerificationRequestRepository : IVerificationRequestRepository
    {
        private readonly IApplicationDbContext context;

        public VerificationRequestRepository(IApplicationDbContext context) => this.context = context;

        private DbSet<VerificationRequest> Set => this.context.VerificationRequests;

        public async Task AddAsync(VerificationRequest entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task<VerificationRequest?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<List<VerificationRequest>> ListByStatusAsync(
            VerificationRequestStatus status, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            return await this.Set.AsNoTracking()
                .Where(x => x.Status == status)
                .Include(x => x.DirectoryEntry)
                .OrderByDescending(x => x.CreateDate)
                .ThenByDescending(x => x.VerificationRequestId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountByStatusAsync(VerificationRequestStatus status, CancellationToken ct = default) =>
            this.Set.AsNoTracking().Where(x => x.Status == status).CountAsync(ct);

        public async Task SetStatusAsync(int id, VerificationRequestStatus status, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            existing.Status = status;
            existing.UpdateDate = DateTime.UtcNow;
            await this.context.SaveChangesAsync(ct);
        }
    }
}