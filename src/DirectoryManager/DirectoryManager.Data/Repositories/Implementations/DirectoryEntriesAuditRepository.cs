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
            await this.context.DirectoryEntriesAudit.AddAsync(entry);
            await this.context.SaveChangesAsync();
        }

        public async Task<IEnumerable<DirectoryEntriesAudit>> GetAuditsForEntryAsync(int directoryEntryId)
        {
            return await this.context.DirectoryEntriesAudit
                                 .Where(dea => dea.DirectoryEntryId == directoryEntryId)
                                 .OrderByDescending(dea => dea.CreateDate)
                                 .ToListAsync();
        }
    }
}