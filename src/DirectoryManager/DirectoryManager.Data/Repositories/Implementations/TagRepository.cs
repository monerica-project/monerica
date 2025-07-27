using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class TagRepository : ITagRepository
    {
        private readonly IApplicationDbContext context;

        public TagRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Find a tag by its primary key.
        /// </summary>
        public async Task<Tag?> GetByIdAsync(int tagId) =>
            await this.context.Tags.FindAsync(tagId);

        /// <summary>
        /// Find a tag by its key.
        /// </summary>
        public async Task<Tag?> GetByKeyAsync(string key) =>
            await this.context.Tags
                          .AsNoTracking()
                          .FirstOrDefaultAsync(t => t.Key == key);

        /// <summary>
        /// List all tags, alphabetically.
        /// </summary>
        public async Task<IReadOnlyList<Tag>> ListAllAsync() =>
            await this.context.Tags
                          .AsNoTracking()
                          .OrderBy(t => t.Name)
                          .ToListAsync();

        /// <summary>
        /// Create a new tag with the given name.
        /// </summary>
        public async Task<Tag> CreateAsync(string name)
        {
            var tag = new Tag { Name = name, Key = name.UrlKey() };
            await this.context.Tags.AddAsync(tag);
            await this.context.SaveChangesAsync();
            return tag;
        }

        /// <summary>
        /// Delete the tag with the given ID, if it exists.
        /// </summary>
        public async Task DeleteAsync(int tagId)
        {
            var tag = await this.context.Tags.FindAsync(tagId);
            if (tag != null)
            {
                this.context.Tags.Remove(tag);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<IReadOnlyList<TagWithLastModified>> ListActiveTagsWithLastModifiedAsync()
        {
            var query = this.context.DirectoryEntryTags
                .AsNoTracking()

                // join through the navigation properties
                .Where(et => et.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)

                // flatten out the bits we need
                .Select(et => new
                {
                    et.TagId,
                    TagName = et.Tag.Name,
                    Slug = et.Tag.Key,
                    Created = et.DirectoryEntry.CreateDate,
                    Updated = et.DirectoryEntry.UpdateDate
                })

                // group by TagId + Name
                .GroupBy(x => new { x.TagId, x.TagName })

                // project into our DTO, computing the MAX() of (Updated ?? Created)
                .Select(g => new TagWithLastModified
                {
                    TagId = g.Key.TagId,
                    Name = g.Key.TagName,
                    LastModified = g.Max(x => x.Updated.HasValue ? x.Updated.Value : x.Created)
                })

                // finally sort by tag name
                .OrderBy(x => x.Name);

            return await query
                .ToListAsync()
                .ConfigureAwait(false);
        }

        // in TagRepository.cs
        public async Task<PagedResult<TagCount>> ListTagsWithCountsPagedAsync(int page, int pageSize)
        {
            // only count non-removed entries
            var query = this.context.DirectoryEntryTags
                .Where(et => et.DirectoryEntry.DirectoryStatus != DirectoryStatus.Removed)
                .GroupBy(et => new { et.TagId, et.Tag.Name, et.Tag.Key })
                .Select(g => new TagCount
                {
                    TagId = g.Key.TagId,
                    Name = g.Key.Name,
                    Key = g.Key.Key,
                    Count = g.Count()
                })
                .OrderBy(tc => tc.Name);

            var total = await query.CountAsync().ConfigureAwait(false);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<TagCount>
            {
                TotalCount = total,
                Items = items
            };
        }
    }
}
