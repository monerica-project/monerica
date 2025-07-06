using DirectoryManager.Data.DbContextInfo;
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
    }
}