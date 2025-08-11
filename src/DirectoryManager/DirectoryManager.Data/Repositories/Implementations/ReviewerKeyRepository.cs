// DirectoryManager.Data/Repositories/ReviewerKeyRepository.cs
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ReviewerKeyRepository : IReviewerKeyRepository
    {
        private readonly IApplicationDbContext context;
        private DbSet<ReviewerKey> Set => this.context.ReviewerKeys; // use typed DbSet from interface

        public ReviewerKeyRepository(IApplicationDbContext context) => this.context = context;

        public IQueryable<ReviewerKey> Query() => this.Set.AsNoTracking();

        public async Task<ReviewerKey?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<ReviewerKey?> GetByFingerprintAsync(string fingerprint, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Fingerprint == fingerprint, ct);

        public async Task<List<ReviewerKey>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenBy(x => x.ReviewerKeyId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task AddAsync(ReviewerKey entity, CancellationToken ct = default)
        {
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(ReviewerKey entity, CancellationToken ct = default)
        {
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null) return;
            this.Set.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }
    }
}
