using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class DirectoryEntryRepository : IDirectoryEntryRepository
    {
        private readonly IDirectoryEntriesAuditRepository directoryEntryAuditRepository;
        private readonly IApplicationDbContext context;

        public DirectoryEntryRepository(
            IApplicationDbContext context,
            IDirectoryEntriesAuditRepository directoryEntryAuditRepository)
        {
            this.directoryEntryAuditRepository = directoryEntryAuditRepository;
            this.context = context;
        }

        public async Task<DirectoryEntry?> GetByIdAsync(int directoryEntryId)
        {
            return await this.context.DirectoryEntries
                .Include(de => de.SubCategory!)
                .ThenInclude(sc => sc.Category!)
                .FirstOrDefaultAsync(de => de.DirectoryEntryId == directoryEntryId);
        }

        public async Task<DirectoryEntry?> GetBySubCategoryAndKeyAsync(int subCategoryId, string directoryEntryKey)
        {
            return await this.context.DirectoryEntries
                .FirstOrDefaultAsync(de => de.SubCategoryId == subCategoryId && de.DirectoryEntryKey == directoryEntryKey);
        }

        public async Task<DirectoryEntry?> GetByLinkAsync(string link)
        {
            return await this.context.DirectoryEntries.FirstOrDefaultAsync(de => de.Link == link);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllowableEntries()
        {
            return await this.context.DirectoryEntries
                .Where(de => de.DirectoryStatus == DirectoryStatus.Admitted ||
                             de.DirectoryStatus == DirectoryStatus.Verified)
                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllAsync()
        {
            if (this.context.DirectoryEntries == null)
            {
                return new List<DirectoryEntry>();
            }

            return await this.context.DirectoryEntries
                .Include(e => e.SubCategory!)
                .ThenInclude(sc => sc.Category)
                .OrderBy(de => de.Name)
                .ToListAsync();
        }

        public async Task CreateAsync(DirectoryEntry entry)
        {
            try
            {
                await this.context.DirectoryEntries.AddAsync(entry);
                await this.context.SaveChangesAsync();
                await this.WriteToAuditLog(entry);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task UpdateAsync(DirectoryEntry entry)
        {
            var existingEntry = await this.context.DirectoryEntries
                .FirstOrDefaultAsync(x => x.DirectoryEntryId == entry.DirectoryEntryId);

            if (existingEntry == null)
            {
                return;
            }

            existingEntry.DirectoryEntryKey = entry.DirectoryEntryKey;
            existingEntry.Name = entry.Name;
            existingEntry.Link = entry.Link;
            existingEntry.Link2 = entry.Link2;
            existingEntry.Link3 = entry.Link3;
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Description = entry.Description;
            existingEntry.Location = entry.Location;
            existingEntry.Processor = entry.Processor;
            existingEntry.Note = entry.Note;
            existingEntry.Contact = entry.Contact;
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.UpdateDate = DateTime.UtcNow;
            existingEntry.UpdatedByUserId = entry.UpdatedByUserId;

            try
            {
                this.context.DirectoryEntries.Update(existingEntry);
                await this.context.SaveChangesAsync();
                await this.WriteToAuditLog(existingEntry);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public async Task DeleteAsync(int directoryEntryId)
        {
            var entryToDelete = await this.context.DirectoryEntries.FindAsync(directoryEntryId);
            if (entryToDelete != null)
            {
                this.context.DirectoryEntries.Remove(entryToDelete);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId)
        {
            return await this.GetActiveEntriesQuery()
                .Where(entry => entry.SubCategoryId == subCategoryId)
                .OrderBy(entry => entry.Name)
                .ToListAsync();
        }

        public DateTime GetLastRevisionDate()
        {
            var latestCreateDate = this.context.DirectoryEntries
                .Max(e => (DateTime?)e.CreateDate);

            var latestUpdateDate = this.context.DirectoryEntries
                .Max(e => e.UpdateDate) ?? DateTime.MinValue;

            return (DateTime)(latestCreateDate > latestUpdateDate ? latestCreateDate : latestUpdateDate);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count)
        {
            return await this.GetActiveEntriesQuery()
                .Where(entry => entry.UpdateDate.HasValue)
                .OrderByDescending(entry => entry.UpdateDate ?? DateTime.MinValue)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count)
        {
            return await this.GetActiveEntriesQuery()
                .OrderByDescending(entry => entry.CreateDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int pageSize, int pageNumber)
        {
            var paginatedEntries = await this.GetActiveEntriesQuery()
                .OrderByDescending(entry => entry.CreateDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return this.GroupByDate(paginatedEntries);
        }

        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays)
        {
            var recentDates = await this.GetActiveEntriesQuery()
                .OrderByDescending(entry => entry.CreateDate)
                .Select(entry => entry.CreateDate.Date)
                .Distinct()
                .Take(numberOfDays)
                .ToListAsync();

            var activeEntries = await this.GetActiveEntriesQuery().ToListAsync();

            return this.GroupByDate(activeEntries.Where(entry => recentDates.Contains(entry.CreateDate.Date)).ToList());
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllActiveEntries()
        {
            return await this.GetActiveEntriesQuery()
                .OrderBy(entry => entry.Name)
                .ToListAsync();
        }

        public async Task<int> TotalActive()
        {
            return await this.GetActiveEntriesQuery().CountAsync();
        }

        public async Task<Dictionary<int, DateTime>> GetLastModifiedDatesBySubCategoryAsync()
        {
            var lastModifiedDates = await this.context.DirectoryEntries
                .GroupBy(de => de.SubCategoryId)
                .Select(g => new
                {
                    SubCategoryId = g.Key,
                    LastModified = g.Max(de => de.UpdateDate ?? de.CreateDate)
                })
                .ToListAsync();

            return lastModifiedDates.ToDictionary(x => x.SubCategoryId, x => x.LastModified);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int subCategoryId)
        {
            // Use the GetActiveEntriesQuery() to ensure only active entries are retrieved.
            return await this.GetActiveEntriesQuery()
                .Where(entry => entry.SubCategoryId == subCategoryId)
                .OrderBy(entry => entry.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync()
        {
            // Retrieve all entries including their related SubCategory and Category.
            return await this.context.DirectoryEntries
                .Include(e => e.SubCategory!)
                .ThenInclude(sc => sc.Category)
                .OrderBy(e => e.Name)
                .ToListAsync();
        }

        private static List<DirectoryStatus> GetNonActiveStatuses()
        {
            return
            [
                DirectoryStatus.Removed,
                DirectoryStatus.Unknown
            ];
        }

        private IQueryable<DirectoryEntry> GetActiveEntriesQuery()
        {
            var nonActiveStatuses = GetNonActiveStatuses();
            return this.context.DirectoryEntries
                .Where(entry => !nonActiveStatuses.Contains(entry.DirectoryStatus));
        }

        private List<GroupedDirectoryEntry> GroupByDate(List<DirectoryEntry> entries)
        {
            return entries
                .GroupBy(entry => entry.CreateDate.Date)
                .OrderByDescending(group => group.Key)
                .Select(group => new GroupedDirectoryEntry
                {
                    Date = group.Key.ToString(Common.Constants.StringConstants.DateFormat),
                    Entries = group.ToList()
                })
                .ToList();
        }

        private async Task WriteToAuditLog(DirectoryEntry? existingEntry)
        {
            if (existingEntry == null)
            {
                return;
            }

            await this.directoryEntryAuditRepository.CreateAsync(new DirectoryEntriesAudit
            {
                Contact = existingEntry.Contact,
                CreateDate = existingEntry.CreateDate,
                Description = existingEntry.Description,
                CreatedByUserId = existingEntry.CreatedByUserId,
                DirectoryStatus = existingEntry.DirectoryStatus,
                DirectoryEntryId = existingEntry.DirectoryEntryId,
                Name = existingEntry.Name,
                SubCategoryId = existingEntry.SubCategoryId,
                UpdateDate = existingEntry.UpdateDate,
                UpdatedByUserId = existingEntry.UpdatedByUserId,
                Link = existingEntry.Link,
                Link2 = existingEntry.Link2,
                Link3 = existingEntry.Link3,
                Location = existingEntry.Location,
                Note = existingEntry.Note,
                Processor = existingEntry.Processor
            });
        }
    }
}