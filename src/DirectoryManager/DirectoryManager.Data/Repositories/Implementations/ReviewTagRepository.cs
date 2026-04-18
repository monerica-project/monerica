using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class ReviewTagRepository : IReviewTagRepository
    {
        private readonly IApplicationDbContext context;
        public ReviewTagRepository(IApplicationDbContext context) => this.context = context;

        public IQueryable<ReviewTag> Query() => this.context.ReviewTags.AsNoTracking();

        public Task<List<ReviewTag>> ListEnabledAsync(CancellationToken ct = default) =>
            this.context.ReviewTags.AsNoTracking()
                .Where(t => t.IsEnabled)
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

        public Task<List<ReviewTag>> ListAllAsync(CancellationToken ct = default) =>
            this.context.ReviewTags.AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync(ct);

        public Task<ReviewTag?> GetByIdAsync(int id, CancellationToken ct = default) =>
            this.context.ReviewTags.FindAsync(new object[] { id }, ct).AsTask();

        public Task<ReviewTag?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
            this.context.ReviewTags.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug, ct);

        public async Task AddAsync(ReviewTag tag, CancellationToken ct = default)
        {
            tag.CreateDate = DateTime.UtcNow;
            this.context.ReviewTags.Add(tag);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(ReviewTag tag, CancellationToken ct = default)
        {
            tag.UpdateDate = DateTime.UtcNow;
            this.context.ReviewTags.Update(tag);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.context.ReviewTags.FindAsync(new object[] { id }, ct);
            if (existing is null) return;
            this.context.ReviewTags.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }
    }
}
