using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryReviewTagRepository : IDirectoryEntryReviewTagRepository
    {
        private readonly IApplicationDbContext context;
        public DirectoryEntryReviewTagRepository(IApplicationDbContext context) => this.context = context;

        public Task<List<int>> GetTagIdsForReviewAsync(int reviewId, CancellationToken ct = default) =>
            this.context.DirectoryEntryReviewTags.AsNoTracking()
                .Where(x => x.DirectoryEntryReviewId == reviewId)
                .Select(x => x.ReviewTagId)
                .ToListAsync(ct);

        public async Task SetTagsForReviewAsync(int reviewId, IReadOnlyCollection<int> tagIds, string? userId, CancellationToken ct = default)
        {
            tagIds ??= Array.Empty<int>();

            var existing = await this.context.DirectoryEntryReviewTags
                .Where(x => x.DirectoryEntryReviewId == reviewId)
                .ToListAsync(ct);

            // remove any not in new set
            var toRemove = existing.Where(x => !tagIds.Contains(x.ReviewTagId)).ToList();
            if (toRemove.Count > 0)
            {
                this.context.DirectoryEntryReviewTags.RemoveRange(toRemove);
            }

            // add missing
            var existingIds = existing.Select(x => x.ReviewTagId).ToHashSet();
            var toAdd = tagIds.Where(id => !existingIds.Contains(id)).ToList();

            foreach (var tagId in toAdd)
            {
                this.context.DirectoryEntryReviewTags.Add(new DirectoryEntryReviewTag
                {
                    DirectoryEntryReviewId = reviewId,
                    ReviewTagId = tagId,
                    CreateDate = DateTime.UtcNow,
                    CreatedByUserId = userId
                });
            }

            await this.context.SaveChangesAsync(ct);
        }
    }
}
