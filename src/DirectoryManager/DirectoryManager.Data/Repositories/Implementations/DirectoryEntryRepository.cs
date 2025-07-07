using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    /// <summary>
    /// Repository for <see cref="DirectoryEntry"/> with eager-loading of Category,
    /// Subcategory and Tags, plus audit-trail writes.
    /// </summary>
    public class DirectoryEntryRepository : IDirectoryEntryRepository
    {
        private readonly IApplicationDbContext context;
        private readonly IDirectoryEntriesAuditRepository auditRepo;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryEntryRepository"/> class.
        /// </summary>
        public DirectoryEntryRepository(
            IApplicationDbContext context,
            IDirectoryEntriesAuditRepository auditRepo)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
        }

        /// <inheritdoc />
        public async Task<DirectoryEntry?> GetByIdAsync(int directoryEntryId)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.DirectoryEntryId == directoryEntryId)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DirectoryEntry?> GetBySubCategoryAndKeyAsync(
            int subCategoryId,
            string directoryEntryKey)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de =>
                    de.SubCategoryId == subCategoryId
                    && de.DirectoryEntryKey == directoryEntryKey)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DirectoryEntry?> GetByLinkAsync(string link)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.Link == link)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<DirectoryEntry?> GetByNameAsync(string name)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.Name == name)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetAllowableAdvertisers()
        {
            return await this.BaseQuery()
                .Where(de =>
                    de.DirectoryStatus == DirectoryStatus.Admitted ||
                    de.DirectoryStatus == DirectoryStatus.Verified)
                .OrderBy(de => de.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetAllAsync()
        {
            return await this.BaseQuery()
                .OrderBy(de => de.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task CreateAsync(DirectoryEntry entry)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            try
            {
                await this.context.DirectoryEntries.AddAsync(entry)
                    .ConfigureAwait(false);
                await this.context.SaveChangesAsync()
                    .ConfigureAwait(false);
                await this.WriteToAuditLogAsync(entry)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex);
            }
        }

        /// <inheritdoc />
        public async Task UpdateAsync(DirectoryEntry entry)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var existing = await this.context.DirectoryEntries
                .FirstOrDefaultAsync(e => e.DirectoryEntryId == entry.DirectoryEntryId)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return;
            }

            existing.Name = entry.Name;
            existing.DirectoryEntryKey = entry.DirectoryEntryKey;
            existing.Link = entry.Link;
            existing.Link2 = entry.Link2;
            existing.Link3 = entry.Link3;
            existing.DirectoryStatus = entry.DirectoryStatus;
            existing.Description = entry.Description;
            existing.Location = entry.Location;
            existing.Processor = entry.Processor;
            existing.Note = entry.Note;
            existing.Contact = entry.Contact;
            existing.SubCategoryId = entry.SubCategoryId;
            existing.UpdateDate = DateTime.UtcNow;
            existing.UpdatedByUserId = entry.UpdatedByUserId;

            try
            {
                this.context.DirectoryEntries.Update(existing);
                await this.context.SaveChangesAsync()
                    .ConfigureAwait(false);
                await this.WriteToAuditLogAsync(existing)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(int directoryEntryId)
        {
            var toRemove = await this.context.DirectoryEntries
                .FindAsync(directoryEntryId)
                .ConfigureAwait(false);

            if (toRemove != null)
            {
                this.context.DirectoryEntries.Remove(toRemove);
                await this.context.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId)
        {
            return await this.ActiveQuery()
                .Where(e => e.SubCategoryId == subCategoryId)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public DateTime GetLastRevisionDate()
        {
            var latestCreate = this.context.DirectoryEntries
                .Max(e => (DateTime?)e.CreateDate) ?? DateTime.MinValue;
            var latestUpdate = this.context.DirectoryEntries
                .Max(e => e.UpdateDate) ?? DateTime.MinValue;

            return latestCreate > latestUpdate
                ? latestCreate
                : latestUpdate;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count)
        {
            return await this.ActiveQuery()
                .Where(e => e.UpdateDate.HasValue)
                .OrderByDescending(e => e.UpdateDate)
                .Take(count)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count)
        {
            return await this.ActiveQuery()
                .OrderByDescending(e => e.CreateDate)
                .Take(count)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(
            int pageSize,
            int pageNumber)
        {
            var page = await this.ActiveQuery()
                .OrderByDescending(e => e.CreateDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);

            return this.GroupByDate(page);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GroupedDirectoryEntry>> GetNewestAdditionsGrouped(
            int numberOfDays)
        {
            var cutoffDates = await this.ActiveQuery()
                .Select(e => e.CreateDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .Take(numberOfDays)
                .ToListAsync()
                .ConfigureAwait(false);

            var all = await this.ActiveQuery()
                .ToListAsync()
                .ConfigureAwait(false);

            var filtered = all
                .Where(e => cutoffDates.Contains(e.CreateDate.Date))
                .ToList();

            return this.GroupByDate(filtered);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetAllActiveEntries()
        {
            return await this.ActiveQuery()
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> TotalActive()
        {
            return await this.ActiveQuery().CountAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, DateTime>> GetLastModifiedDatesBySubCategoryAsync()
        {
            var list = await this.context.DirectoryEntries
                .GroupBy(e => e.SubCategoryId)
                .Select(g => new
                {
                    SubCategoryId = g.Key,
                    LastModified = g.Max(x => x.UpdateDate ?? x.CreateDate)
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return list.ToDictionary(x => x.SubCategoryId, x => x.LastModified);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByStatusAsync(
            DirectoryStatus status)
        {
            return await this.BaseQuery()
                .Where(e => e.DirectoryStatus == status)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<MonthlyDirectoryEntries> GetEntriesCreatedForPreviousMonthWithMonthKeyAsync()
        {
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            var end = start.AddMonths(1);

            var entries = await this.ActiveQuery()
                .Where(e => e.CreateDate >= start && e.CreateDate < end)
                .OrderBy(e => e.CreateDate)
                .ToListAsync()
                .ConfigureAwait(false);

            return new MonthlyDirectoryEntries
            {
                MonthKey = start.ToString(Common.Constants.StringConstants.YearMonth),
                Entries = entries
            };
        }

        /// <inheritdoc />
        public async Task<WeeklyDirectoryEntries> GetEntriesCreatedForPreviousWeekWithWeekKeyAsync()
        {
            var now = DateTime.UtcNow;
            int daysSinceMon = (int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1;
            var startOfWeek = now.Date.AddDays(-daysSinceMon - 7);
            var endOfWeek = startOfWeek.AddDays(7);

            var entries = await this.ActiveQuery()
                .Where(e => e.CreateDate >= startOfWeek && e.CreateDate < endOfWeek)
                .OrderByDescending(e => e.CreateDate)
                .ToListAsync()
                .ConfigureAwait(false);

            return new WeeklyDirectoryEntries
            {
                WeekStartDate = startOfWeek.ToString(Common.Constants.StringConstants.DateFormat),
                Entries = entries
            };
        }

        public async Task<PagedResult<DirectoryEntry>> SearchAsync(
            string term,
            int page,
            int pageSize)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new PagedResult<DirectoryEntry>();
            }

            term = term.Trim().ToLowerInvariant();
            string pattern = $"%{term}%";

            // 1) server-side filter on any matching field, only non-removed
            var filtered = this.BaseQuery()
                .Where(e =>
                    e.DirectoryStatus != DirectoryStatus.Removed &&
                    (
                        EF.Functions.Like(e.Name.ToLower(), pattern) ||
                        EF.Functions.Like((e.Description ?? "").ToLower(), pattern) ||
                        EF.Functions.Like(e.SubCategory!.Name.ToLower(), pattern) ||
                        EF.Functions.Like(e.SubCategory.Category!.Name.ToLower(), pattern) ||
                        e.EntryTags.Any(et => EF.Functions.Like(et.Tag.Name.ToLower(), pattern)) ||
                        EF.Functions.Like((e.Note ?? "").ToLower(), pattern) ||
                        EF.Functions.Like((e.Processor ?? "").ToLower(), pattern) ||
                        EF.Functions.Like((e.Location ?? "").ToLower(), pattern) ||
                        EF.Functions.Like((e.Contact ?? "").ToLower(), pattern) ||
                        EF.Functions.Like((e.Link ?? "").ToLower(), pattern)
                    ));

            // 2) load into memory for scoring
            var candidates = await filtered.ToListAsync();

            static int CountOcc(string? f, string term)
            {
                if (string.IsNullOrEmpty(f)) return 0;
                var txt = f.ToLowerInvariant();
                int count = 0, idx = 0;
                while ((idx = txt.IndexOf(term, idx, StringComparison.Ordinal)) != -1)
                {
                    count++;
                    idx += term.Length;
                }
                return count;
            }

            // 3) compute scores and status weight
            var scored = candidates
                .Select(e =>
                {
                    var score =
                        CountOcc(e.Name, term) +
                        CountOcc(e.Description, term) +
                        CountOcc(e.SubCategory?.Name, term) +
                        CountOcc(e.SubCategory?.Category?.Name, term) +
                        e.EntryTags.Sum(et => CountOcc(et.Tag.Name, term)) +
                        CountOcc(e.Note, term) +
                        CountOcc(e.Processor, term) +
                        CountOcc(e.Location, term) +
                        CountOcc(e.Contact, term) +
                        CountOcc(e.Link, term);

                    // weight: Verified=4, Admitted=3, Questionable=2, Scam=1
                    int statusWeight = e.DirectoryStatus switch
                    {
                        DirectoryStatus.Verified => 4,
                        DirectoryStatus.Admitted => 3,
                        DirectoryStatus.Questionable => 2,
                        DirectoryStatus.Scam => 1,
                        _ => 0
                    };

                    return new { Entry = e, Score = score, Weight = statusWeight };
                })
                .Where(x => x.Score > 0)
                // 4) order by status weight first, then by score
                .OrderByDescending(x => x.Weight)
                .ThenByDescending(x => x.Score)
                .ToList();

            // 5) page
            int total = scored.Count;
            var pageEntries = scored
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Entry)
                .ToList();

            return new PagedResult<DirectoryEntry>
            {
                TotalCount = total,
                Items = pageEntries
            };
        }



        /// <summary>
        /// Base query including SubCategory→Category and EntryTags→Tag.
        /// </summary>
        private IQueryable<DirectoryEntry> BaseQuery()
        {
            return this.context.DirectoryEntries
                .Include(e => e.SubCategory!)
                    .ThenInclude(sc => sc.Category!)
                .Include(e => e.EntryTags)
                    .ThenInclude(et => et.Tag);
        }

        /// <summary>
        /// Filters out Removed and Unknown statuses.
        /// </summary>
        private IQueryable<DirectoryEntry> ActiveQuery()
        {
            var nonActive = new[] { DirectoryStatus.Removed, DirectoryStatus.Unknown };
            return this.BaseQuery()
                .Where(e => !nonActive.Contains(e.DirectoryStatus));
        }

        /// <summary>
        /// Groups a list of entries by their CreateDate (date-only).
        /// </summary>
        private List<GroupedDirectoryEntry> GroupByDate(
            List<DirectoryEntry> list)
        {
            return list
                .GroupBy(e => e.CreateDate.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new GroupedDirectoryEntry
                {
                    Date = g.Key.ToString(Common.Constants.StringConstants.DateFormat),
                    Entries = g.ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Writes a snapshot of <paramref name="entry"/> to the audit log.
        /// </summary>
        private async Task WriteToAuditLogAsync(DirectoryEntry entry)
        {
            if (entry is null)
            {
                return;
            }

            await this.auditRepo.CreateAsync(new DirectoryEntriesAudit
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                Name = entry.Name,
                Link = entry.Link,
                Link2 = entry.Link2,
                Link3 = entry.Link3,
                Description = entry.Description,
                Location = entry.Location,
                Processor = entry.Processor,
                Note = entry.Note,
                Contact = entry.Contact,
                DirectoryStatus = entry.DirectoryStatus,
                SubCategoryId = entry.SubCategoryId,
                CreateDate = entry.CreateDate,
                UpdateDate = entry.UpdateDate,
                CreatedByUserId = entry.CreatedByUserId,
                UpdatedByUserId = entry.UpdatedByUserId
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesBySubcategoryAsync(int subCategoryId)
        {
            return await this.ActiveQuery()
                .Where(e => e.SubCategoryId == subCategoryId)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByCategoryAsync(int categoryId)
        {
            return await this.ActiveQuery()
                .Where(e => e.SubCategory!.CategoryId == categoryId)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<DirectoryEntry>> GetAllEntitiesAndPropertiesAsync()
        {
            return await this.BaseQuery()
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
