using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryTagRepository : IDirectoryEntryTagRepository
    {
        private readonly IApplicationDbContext context;

        public DirectoryEntryTagRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task AssignTagAsync(int entryId, int tagId)
        {
            bool exists = await this.context.DirectoryEntryTags
                .AnyAsync(et => et.DirectoryEntryId == entryId && et.TagId == tagId);

            if (!exists)
            {
                this.context.DirectoryEntryTags.Add(new DirectoryEntryTag
                {
                    DirectoryEntryId = entryId,
                    TagId = tagId
                });
                await this.context.SaveChangesAsync();
            }
        }

        public async Task RemoveTagAsync(int entryId, int tagId)
        {
            var link = await this.context.DirectoryEntryTags
                .FirstOrDefaultAsync(et => et.DirectoryEntryId == entryId && et.TagId == tagId);

            if (link != null)
            {
                this.context.DirectoryEntryTags.Remove(link);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<Tag>> GetTagsForEntryAsync(int entryId)
        {
            return await this.context.DirectoryEntryTags
                .Where(et => et.DirectoryEntryId == entryId)
                .Include(et => et.Tag)
                .Select(et => et.Tag!)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DirectoryEntry>> ListEntriesForTagAsync(string tagName)
        {
            return await this.context.DirectoryEntryTags
                .Include(et => et.DirectoryEntry)
                    .ThenInclude(de => de.SubCategory!)
                    .ThenInclude(sc => sc.Category!)
                .Include(et => et.Tag)
                .Where(et => et.Tag!.Name == tagName)
                .Select(et => et.DirectoryEntry!)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> CountByTagAsync(int tagId)
        {
            return await this.context.DirectoryEntryTags
                .Where(et => et.TagId == tagId && et.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .Select(et => et.DirectoryEntryId)
                .Distinct()
                .CountAsync();
        }

        public async Task<PagedResult<DirectoryEntry>> ListEntriesForTagPagedAsync(
            string tagName,
            int page,
            int pageSize)
        {
            // 1) Start from DirectoryEntries so we can Include navigations
            var query = this.context.DirectoryEntries
                .Include(e => e.SubCategory)
                    .ThenInclude(sc => sc.Category)
                .Include(e => e.EntryTags)
                    .ThenInclude(et => et.Tag)
                .Where(e =>
                    e.DirectoryStatus != DirectoryStatus.Removed &&
                    e.EntryTags.Any(et => et.Tag.Name == tagName))
                .OrderBy(e => e.Name);

            // 2) Count then page
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<DirectoryEntry>
            {
                TotalCount = total,
                Items = items
            };
        }

    }
}