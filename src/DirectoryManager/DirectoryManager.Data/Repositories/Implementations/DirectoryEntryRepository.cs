using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryRepository : IDirectoryEntryRepository
    {
        private readonly ApplicationDbContext _context;

        public DirectoryEntryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DirectoryEntry> GetByIdAsync(int id)
        {
            return await _context.DirectoryEntries.FindAsync(id);
        }

        public async Task<DirectoryEntry> GetByLinkAsync(string link)
        {
            return await _context.DirectoryEntries.FirstOrDefaultAsync(de => de.Link == link);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllAsync()
        {
            // Ensure that the DbSet DirectoryEntries is not null.
            if (_context.DirectoryEntries == null)
            {
                return new List<DirectoryEntry>();
            }

            // Include both SubCategory and its related Category.
            return await _context.DirectoryEntries
                                 .Include(e => e.SubCategory)
                                 .ThenInclude(sc => sc.Category)
                                 .OrderBy(de => de.Name)
                                 .ToListAsync();
        }

        public async Task CreateAsync(DirectoryEntry entry)
        {
            await _context.DirectoryEntries.AddAsync(entry);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DirectoryEntry entry)
        {
            var existingEntry = await _context.DirectoryEntries.FindAsync(entry.Id);
            _context.DirectoryEntries.Update(existingEntry);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var entryToDelete = await _context.DirectoryEntries.FindAsync(id);
            if (entryToDelete != null)
            {
                _context.DirectoryEntries.Remove(entryToDelete);
                await _context.SaveChangesAsync();
            }
        }


        public async Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId)
        {
            return await _context.DirectoryEntries
                                 .Where(e => e.SubCategory.Id == subCategoryId)
                                 .OrderBy(e => e.Name)
                                 .ToListAsync();
        }

        public DateTime GetLastRevisionDate()
        {
            // Fetch the latest CreateDate and UpdateDate
            var latestCreateDate = _context.DirectoryEntries.Max(e => e.CreateDate);
            var latestUpdateDate = _context.DirectoryEntries.Max(e => e.UpdateDate);

            if (latestCreateDate == null && latestUpdateDate == null)
            {
                return DateTime.MinValue;
            }

            // Return the more recent of the two dates
            return (DateTime)(latestCreateDate > latestUpdateDate ? latestCreateDate : latestUpdateDate);
        }
        public async Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count)
        {
            return await _context.DirectoryEntries
                .OrderByDescending(entry => (entry.UpdateDate.HasValue && entry.UpdateDate.Value > entry.CreateDate)
                                             ? entry.UpdateDate.Value
                                             : entry.CreateDate)
                .Take(count)
                .ToListAsync();

        }

        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int subCategoryId)
        {
            return await _context.DirectoryEntries
                            .Where(entry => entry.SubCategoryId == subCategoryId &&
                                        entry.DirectoryStatus != DirectoryStatus.Removed &&
                                        entry.DirectoryStatus != DirectoryStatus.Unknown)
                                .OrderByDescending(entry => entry.Name)
                                .ToListAsync();
        }
    }
}
