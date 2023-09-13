using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntriesAuditRepository : IDirectoryEntriesAuditRepository
    {
        private readonly ApplicationDbContext _context;

        public DirectoryEntriesAuditRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DirectoryEntriesAudit>> GetAllAsync()
        {
            // Ensure that the DbSet DirectoryEntries is not null.
            if (_context.DirectoryEntries == null)
            {
                return new List<DirectoryEntriesAudit>();
            }

            // Include both SubCategory and its related Category.
            return await _context.DirectoryEntriesAudit
                                 .OrderBy(de => de.Name)
                                 .ToListAsync();
        }

        public async Task CreateAsync(DirectoryEntriesAudit entry)
        {
            await _context.DirectoryEntriesAudit.AddAsync(entry);
            await _context.SaveChangesAsync();
        }
 
         
    }
}
