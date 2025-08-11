// DirectoryManager.Data/Repositories/Implementations/DirectoryEntryReviewRepository.cs
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryReviewRepository : IDirectoryEntryReviewRepository
    {
        private readonly IApplicationDbContext context;

        // Use the typed DbSet exposed on the interface
        private DbSet<DirectoryEntryReview> Set => this.context.DirectoryEntryReviews;

        public DirectoryEntryReviewRepository(IApplicationDbContext context) => this.context = context;

        public IQueryable<DirectoryEntryReview> Query() =>
            this.Set.AsNoTracking();

        public async Task<DirectoryEntryReview?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<List<DirectoryEntryReview>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .ThenBy(x => x.DirectoryEntryReviewId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task AddAsync(DirectoryEntryReview entity, CancellationToken ct = default)
        {
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(DirectoryEntryReview entity, CancellationToken ct = default)
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

        // Optional helpers — use strongly-typed properties if your model has them
        public async Task<List<DirectoryEntryReview>> ListForEntryAsync(
            int directoryEntryId, int page = 1, int pageSize = 50, CancellationToken ct = default) =>
            await this.Set.AsNoTracking()
                .Where(r => r.DirectoryEntryId == directoryEntryId)   // <-- uses your property
                .OrderByDescending(x => x.UpdateDate ?? x.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public async Task<double?> AverageRatingForEntryAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var q = this.Set
                .Where(r => r.DirectoryEntryId == directoryEntryId)
                .Select(r => (int?)r.Rating); // assumes int? Rating exists
            if (!await q.AnyAsync(ct)) return null;
            return await q.AverageAsync(x => (double?)x, ct);
        }
    }
}
