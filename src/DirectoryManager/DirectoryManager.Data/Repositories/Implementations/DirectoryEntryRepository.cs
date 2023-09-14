using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryRepository : IDirectoryEntryRepository
    {
        IDirectoryEntriesAuditRepository _directoryEntryAuditRepository;
        private readonly ApplicationDbContext _context;

        public DirectoryEntryRepository(ApplicationDbContext context,
            IDirectoryEntriesAuditRepository directoryEntryAuditRepository)
        {
            _directoryEntryAuditRepository = directoryEntryAuditRepository;
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

            await _directoryEntryAuditRepository.CreateAsync(
                new DirectoryEntriesAudit
                {
                    Contact = existingEntry.Contact,
                    CreateDate = existingEntry.CreateDate,
                    Description = existingEntry.Description,
                    CreatedByUserId = existingEntry.CreatedByUserId,
                    DirectoryStatus = existingEntry.DirectoryStatus,
                    Id = existingEntry.Id,
                    Link = existingEntry.Link,
                    Name = existingEntry.Name,
                    SubCategoryId = existingEntry.SubCategoryId,
                    UpdateDate = existingEntry.UpdateDate,
                    UpdatedByUserId = existingEntry.UpdatedByUserId,
                    Link2 = existingEntry.Link2,
                    Location = existingEntry.Location,
                    Note = existingEntry.Note,
                    Processor = existingEntry.Processor

                });
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
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => (entry.UpdateDate.HasValue && entry.UpdateDate.Value > entry.CreateDate)
                                             ? entry.UpdateDate.Value
                                             : entry.CreateDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count)
        {
            return await _context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => entry.CreateDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays)
        {
            var recentDates = await _context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => entry.CreateDate)
                .Select(entry => entry.CreateDate.Date)
                .Distinct()
                .Take(numberOfDays)
                .ToListAsync();

            // Retrieve all entries and perform filtering and grouping on the client side
            var allEntries = await _context.DirectoryEntries
                .ToListAsync();

            var groupedEntries = allEntries
                .Where(entry => recentDates.Contains(entry.CreateDate.Date))
                .GroupBy(entry => entry.CreateDate.Date)
                .OrderByDescending(group => group.Key)
                .Select(dateGroup => new GroupedDirectoryEntry
                {
                    Date = dateGroup.Key.ToString("yyyy-MM-dd"), // Convert date to string
                    Entries = dateGroup
                        .Select(entry => new DirectoryEntry
                        {
                            Name = entry.Name,
                            Link = entry.Link,
                            Description = entry.Description
                        })
                        .ToList()
                })
                .ToList();

            return groupedEntries;
        }


        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int subCategoryId)
        {
            return await _context.DirectoryEntries
                            .Where(entry => entry.SubCategoryId == subCategoryId &&
                                        entry.DirectoryStatus != DirectoryStatus.Removed &&
                                        entry.DirectoryStatus != DirectoryStatus.Unknown)
                                .OrderBy(entry => entry.Name)
                                .ToListAsync();
        }


        public async Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync()
        {
            return await _context.DirectoryEntries
                .Include(e => e.SubCategory)
                    .ThenInclude(sc => sc.Category)
                .OrderBy(de => de.Name)
                .ToListAsync();
        }

    }
}
