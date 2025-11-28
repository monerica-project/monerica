using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntriesAuditRepository : IDirectoryEntriesAuditRepository
    {
        private readonly IApplicationDbContext context;

        public DirectoryEntriesAuditRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<DirectoryEntriesAudit>> GetAllAsync()
        {
            // Ensure that the DbSet DirectoryEntries is not null.
            if (this.context.DirectoryEntries == null)
            {
                return new List<DirectoryEntriesAudit>();
            }

            // Include both SubCategory and its related Category.
            return await this.context.DirectoryEntriesAudit
                                 .OrderBy(de => de.Name)
                                 .ToListAsync();
        }

        public async Task CreateAsync(DirectoryEntriesAudit entry)
        {
            if (entry.UpdateDate == null)
            {
                entry.UpdateDate = DateTime.UtcNow;
            }

            await this.context.DirectoryEntriesAudit.AddAsync(entry);
            await this.context.SaveChangesAsync();
        }

        public async Task<IEnumerable<DirectoryEntriesAudit>> GetAuditsWithSubCategoriesForEntryAsync(int entryId)
        {
            return await this.context.DirectoryEntriesAudit
                .Where(audit => audit.DirectoryEntryId == entryId)
                .Include(audit => audit.SubCategory!)
                .ThenInclude(subCategory => subCategory.Category!)
                .ToListAsync();
        }

        public async Task<List<DirectoryEntriesAudit>> GetAllWithSubcategoriesAsync(DateTime fromUtc, DateTime toUtc)
        {
            return await this.context.DirectoryEntriesAudit
                .Include(a => a.SubCategory).ThenInclude(sc => sc.Category)
                .Where(a =>
                    (a.UpdateDate ?? a.CreateDate) >= fromUtc &&
                    (a.UpdateDate ?? a.CreateDate) <= toUtc)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}