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
            return await this.context.DirectoryEntries.FindAsync(directoryEntryId);
        }

        public async Task<DirectoryEntry?> GetByLinkAsync(string link)
        {
            return await this.context.DirectoryEntries.FirstOrDefaultAsync(de => de.Link == link);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllowableEntries()
        {
            return await this.context.DirectoryEntries
                        .Where(de =>
                                de.DirectoryStatus == DirectoryStatus.Admitted ||
                                de.DirectoryStatus == DirectoryStatus.Verified)
                        .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllAsync()
        {
            // Ensure that the DbSet DirectoryEntries is not null.
            if (this.context.DirectoryEntries == null)
            {
                return new List<DirectoryEntry>();
            }

            // Include both SubCategory and its related Category.
            return await this.context.DirectoryEntries
                                    .Select(e => new DirectoryEntry
                                    {
                                        // Map other properties of DirectoryEntry as needed
                                        Name = e.Name,
                                        Link = e.Link,
                                        Description = e.Description,
                                        DirectoryStatus = e.DirectoryStatus,
                                        Location = e.Location,
                                        Note = e.Note,
                                        Processor = e.Processor,
                                        Contact = e.Contact,
                                        Link2 = e.Link2,
                                        CreateDate = e.CreateDate,
                                        UpdateDate = e.UpdateDate,
                                        CreatedByUserId = e.CreatedByUserId,
                                        UpdatedByUserId = e.UpdatedByUserId,
                                        DirectoryEntryId = e.DirectoryEntryId,
                                        SubCategoryId = e.SubCategoryId,

                                        SubCategory = e.SubCategory == null ? null : new Subcategory
                                        {
                                            // Map other properties of SubCategory as needed
                                            Name = e.SubCategory.Name,
                                            Category = e.SubCategory.Category,
                                            CategoryId = e.SubCategory.CategoryId,
                                            SubCategoryId = e.SubCategory.SubCategoryId,
                                            SubCategoryKey = e.SubCategory.SubCategoryKey,
                                            Description = e.SubCategory.Description,
                                            Note = e.SubCategory.Note,
                                            CreateDate = e.SubCategory.CreateDate,
                                            UpdateDate = e.SubCategory.UpdateDate,
                                            CreatedByUserId = e.SubCategory.CreatedByUserId,
                                            UpdatedByUserId = e.SubCategory.UpdatedByUserId
                                        }
                                    })
                                    .OrderBy(de => de.Name)
                                    .ToListAsync();
        }

        public async Task CreateAsync(DirectoryEntry entry)
        {
            await this.context.DirectoryEntries.AddAsync(entry);
            await this.context.SaveChangesAsync();
            await this.WriteToAuditLog(entry);
        }

        public async Task UpdateAsync(DirectoryEntry entry)
        {
            var existingEntry = await this.context.DirectoryEntries.FirstOrDefaultAsync(x => x.DirectoryEntryId == entry.DirectoryEntryId);

            if (existingEntry == null)
            {
                return;
            }

            existingEntry.Name = entry.Name;
            existingEntry.Link = entry.Link;
            existingEntry.Link2 = entry.Link2;
            existingEntry.DirectoryStatus = entry.DirectoryStatus;
            existingEntry.Description = entry.Description;
            existingEntry.Location = entry.Location;
            existingEntry.Processor = entry.Processor;
            existingEntry.Note = entry.Note;
            existingEntry.Contact = entry.Contact;
            existingEntry.SubCategoryId = entry.SubCategoryId;
            existingEntry.UpdateDate = DateTime.UtcNow;
            existingEntry.UpdatedByUserId = entry.UpdatedByUserId;

            this.context.DirectoryEntries.Update(existingEntry);
            await this.context.SaveChangesAsync();
            await this.WriteToAuditLog(existingEntry);
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
            return await this.context.DirectoryEntries
                                 .Where(e => e.SubCategory != null && e.SubCategory.SubCategoryId == subCategoryId)
                                 .OrderBy(e => e.Name)
                                 .ToListAsync();
        }

        public DateTime GetLastRevisionDate()
        {
            // Fetch the latest CreateDate and UpdateDate
            var latestCreateDate = this.context.DirectoryEntries
                                   .Where(e => e != null)
                                   .Max(e => (DateTime?)e.CreateDate);

            var latestUpdateDate = this.context.DirectoryEntries
                                   .Where(e => e != null)
                                   .Max(e => e.UpdateDate) ?? DateTime.MinValue;

            // Return the more recent of the two dates
            return (DateTime)(latestCreateDate > latestUpdateDate ? latestCreateDate : latestUpdateDate);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count)
        {
            return await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown &&
                            x.UpdateDate.HasValue)
                .OrderByDescending(entry => entry.UpdateDate ?? DateTime.MinValue)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count)
        {
            return await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => entry.CreateDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int pageSize, int pageNumber)
        {
            // Get entries for pagination
            var paginatedEntries = await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => entry.CreateDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var groupedEntries = paginatedEntries
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

        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(int numberOfDays)
        {
            var recentDates = await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .OrderByDescending(entry => entry.CreateDate)
                .Select(entry => entry.CreateDate.Date)
                .Distinct()
                .Take(numberOfDays)
                .ToListAsync();

            // Retrieve all entries and perform filtering and grouping on the client side
            var allEntries = await this.context.DirectoryEntries
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
            return await this.context.DirectoryEntries
                            .Where(entry => entry.SubCategoryId == subCategoryId &&
                                        entry.DirectoryStatus != DirectoryStatus.Removed &&
                                        entry.DirectoryStatus != DirectoryStatus.Unknown)
                                .OrderBy(entry => entry.Name)
                                .ToListAsync();
        }

        public async Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync()
        {
            return await this.context.DirectoryEntries
                .Include(e => e.SubCategory!)
                .ThenInclude(sc => sc.Category)
                .OrderBy(de => de.Name)
                .ToListAsync();
        }

        public async Task<int> TotalActive()
        {
            return await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed &&
                            x.DirectoryStatus != DirectoryStatus.Unknown)
                .CountAsync();
        }

        private async Task WriteToAuditLog(DirectoryEntry? existingEntry)
        {
            if (existingEntry == null)
            {
                return;
            }

            await this.directoryEntryAuditRepository.CreateAsync(
                new DirectoryEntriesAudit
                {
                    Contact = existingEntry.Contact,
                    CreateDate = existingEntry.CreateDate,
                    Description = existingEntry.Description,
                    CreatedByUserId = existingEntry.CreatedByUserId,
                    DirectoryStatus = existingEntry.DirectoryStatus,
                    DirectoryEntryId = existingEntry.DirectoryEntryId,
                    Link = existingEntry.Link,
                    Name = existingEntry.Name,
                    SubCategoryId = existingEntry.SubCategoryId,
                    UpdateDate = existingEntry.UpdateDate,
                    UpdatedByUserId = existingEntry.UpdatedByUserId,
                    Link2 = existingEntry.Link2,
                    Link3 = existingEntry.Link3,
                    Location = existingEntry.Location,
                    Note = existingEntry.Note,
                    Processor = existingEntry.Processor
                });
        }
    }
}