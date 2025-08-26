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

        public async Task<DirectoryEntry?> GetByIdAsync(int directoryEntryId)
        {
               return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.DirectoryEntryId == directoryEntryId)
                .ConfigureAwait(false);
        }

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

        public async Task<DirectoryEntry?> GetByLinkAsync(string link)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.Link == link)
                .ConfigureAwait(false);
        }

        public async Task<DirectoryEntry?> GetByNameAsync(string name)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.Name == name)
                .ConfigureAwait(false);
        }

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

        public async Task<IEnumerable<DirectoryEntry>> GetAllAsync()
        {
            return await this.BaseQuery()
                .OrderBy(de => de.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

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
            existing.PgpKey = entry.PgpKey;
            existing.CountryCode = entry.CountryCode;

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

        public async Task<IEnumerable<DirectoryEntry>> GetAllBySubCategoryIdAsync(int subCategoryId)
        {
            return await this.ActiveQuery()
                .Where(e => e.SubCategoryId == subCategoryId)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

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

        public async Task<IEnumerable<DirectoryEntry>> GetNewestRevisions(int count)
        {
            return await this.ActiveQuery()
                .Where(e => e.UpdateDate.HasValue)
                .OrderByDescending(e => e.UpdateDate)
                .Take(count)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<DirectoryEntry>> GetNewestAdditions(int count)
        {
            // Phase 1: just fetch the IDs of the newest 'count' entries
            var ids = await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed)
                .AsNoTracking()
                .OrderByDescending(e => e.CreateDate)
                .Take(count)
                .Select(e => e.DirectoryEntryId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (!ids.Any())
            {
                return Array.Empty<DirectoryEntry>();
            }

            // Phase 2: eager‐load the full entities (and their SubCategory/Category/Tags)
            var entries = await this.BaseQuery()
                .AsNoTracking()
                .Where(e => ids.Contains(e.DirectoryEntryId))
                .ToListAsync()
                .ConfigureAwait(false);

            // Finally, re‐order them by CreateDate descending
            return entries
                .OrderByDescending(e => e.CreateDate)
                .ToList();
        }

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

        public async Task<IEnumerable<DirectoryEntry>> GetAllActiveEntries()
        {
            return await this.ActiveQuery()
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> TotalActive()
        {
            return await this.ActiveQuery().CountAsync()
                .ConfigureAwait(false);
        }

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

        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesByStatusAsync(
            DirectoryStatus status)
        {
            return await this.BaseQuery()
                .Where(e => e.DirectoryStatus == status)
                .OrderBy(e => e.Name)
                .ToListAsync()
                .ConfigureAwait(false);
        }

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

        public async Task<PagedResult<DirectoryEntry>> SearchAsync(string term, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new PagedResult<DirectoryEntry>();
            }

            term = term.Trim().ToLowerInvariant();

            string? countryCode = Utilities.Helpers.CountryHelper.ExtractCountryCode(term);

            var primaryPattern = $"%{term}%";
            string? rootTerm = null;
            string? rootPattern = null;

            if (term.EndsWith("s") && term.Length > 3)
            {
                rootTerm = term[..^1];
                rootPattern = $"%{rootTerm}%";
            }

            // 1) server-side filter
            var filtered = this.BaseQuery()
                .Where(e =>
                    e.DirectoryStatus != DirectoryStatus.Removed &&
                    (

                        // Country
                        (countryCode != null && e.CountryCode == countryCode)

                        // Name
                        || EF.Functions.Like(e.Name.ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like(e.Name.ToLower(), rootPattern))

                        // Description
                        || EF.Functions.Like((e.Description ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Description ?? "").ToLower(), rootPattern))

                        // Subcategory / Category
                        || EF.Functions.Like(e.SubCategory!.Name.ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like(e.SubCategory.Name.ToLower(), rootPattern))
                        || EF.Functions.Like(e.SubCategory.Category!.Name.ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like(e.SubCategory.Category.Name.ToLower(), rootPattern))

                        // Tags
                        || e.EntryTags.Any(et =>
                            EF.Functions.Like(et.Tag.Name.ToLower(), primaryPattern) ||
                            (rootPattern != null && EF.Functions.Like(et.Tag.Name.ToLower(), rootPattern)))

                        // Note, Processor, Location, Contact
                        || EF.Functions.Like((e.Note ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Note ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.Processor ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Processor ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.Location ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Location ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.Contact ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Contact ?? "").ToLower(), rootPattern))

                        // Links
                        || EF.Functions.Like((e.Link ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Link ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.Link2 ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Link2 ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.Link3 ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.Link3 ?? "").ToLower(), rootPattern))
                        || EF.Functions.Like((e.ProofLink ?? "").ToLower(), primaryPattern)
                        || (rootPattern != null && EF.Functions.Like((e.ProofLink ?? "").ToLower(), rootPattern))));

            // 2) in-memory scoring
            var candidates = await filtered.ToListAsync();

            static int CountOcc(string? field, string t)
            {
                if (string.IsNullOrEmpty(field))
                {
                    return 0;
                }

                var txt = field.ToLowerInvariant();
                int count = 0, idx = 0;
                while ((idx = txt.IndexOf(t, idx, StringComparison.Ordinal)) != -1)
                {
                    count++;
                    idx += t.Length;
                }

                return count;
            }

            const int CountryBoost = 7; // tune this to push country matches up

            var scored = candidates
                .Select(e =>
                {
                    int hits = CountOcc(e.Name, term)
                             + (rootTerm != null ? CountOcc(e.Name, rootTerm) : 0)
                             + CountOcc(e.Description, term)
                             + (rootTerm != null ? CountOcc(e.Description, rootTerm) : 0)
                             + CountOcc(e.SubCategory?.Name, term)
                             + (rootTerm != null ? CountOcc(e.SubCategory?.Name, rootTerm) : 0)
                             + CountOcc(e.SubCategory?.Category?.Name, term)
                             + (rootTerm != null ? CountOcc(e.SubCategory?.Category?.Name, rootTerm) : 0)
                             + e.EntryTags.Sum(et => CountOcc(et.Tag.Name, term) + (rootTerm != null ? CountOcc(et.Tag.Name, rootTerm) : 0))
                             + CountOcc(e.Note, term)
                             + (rootTerm != null ? CountOcc(e.Note, rootTerm) : 0)
                             + CountOcc(e.Processor, term)
                             + (rootTerm != null ? CountOcc(e.Processor, rootTerm) : 0)
                             + CountOcc(e.Location, term)
                             + (rootTerm != null ? CountOcc(e.Location, rootTerm) : 0)
                             + CountOcc(e.Contact, term)
                             + (rootTerm != null ? CountOcc(e.Contact, rootTerm) : 0)
                             + CountOcc(e.Link, term)
                             + (rootTerm != null ? CountOcc(e.Link, rootTerm) : 0)
                             + CountOcc(e.Link2, term)
                             + (rootTerm != null ? CountOcc(e.Link2, rootTerm) : 0)
                             + CountOcc(e.Link3, term)
                             + (rootTerm != null ? CountOcc(e.Link3, rootTerm) : 0)
                             + CountOcc(e.ProofLink, term)
                             + (rootTerm != null ? CountOcc(e.ProofLink, rootTerm) : 0);

                    bool countryMatch = countryCode != null && string.Equals(e.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase);

                    int weight = e.DirectoryStatus switch
                    {
                        DirectoryStatus.Verified => 4,
                        DirectoryStatus.Admitted => 3,
                        DirectoryStatus.Questionable => 2,
                        DirectoryStatus.Scam => 1,
                        _ => 0,
                    };

                    int score = hits + (countryMatch ? CountryBoost : 0);
                    return new { Entry = e, Score = score, Hits = hits, Weight = weight, CountryMatch = countryMatch };
                })

                // include items that only matched by country
                .Where(x => x.Hits > 0 || x.CountryMatch)
                .OrderByDescending(x => x.Weight)
                .ThenByDescending(x => x.Score)
                .ToList();

            // 4) paging
            int total = scored.Count;
            var items = scored.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Entry).ToList();

            return new PagedResult<DirectoryEntry> { TotalCount = total, Items = items };
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<DirectoryEntry>> GetActiveEntriesBySubcategoryAsync(int subCategoryId)
        {
            try
            {
                return await this.ActiveQuery()
                    .Where(e => e.SubCategoryId == subCategoryId)
                    .OrderBy(e => e.Name)
                    .ToListAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }
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

        public async Task<PagedResult<DirectoryEntry>> ListEntriesByCategoryAsync(int categoryId, int page, int pageSize)
        {
            // base query: only non-removed, matching category
            var query = this.context.DirectoryEntries
                .Include(e => e.SubCategory)
                .ThenInclude(sc => sc.Category)
                .Where(e =>
                    e.DirectoryStatus != DirectoryStatus.Removed &&
                    (e.SubCategory != null &&
                    e.SubCategory.CategoryId == categoryId))

                // order by subcategory name then entry name
                .OrderBy(e => e.SubCategory!.Name)
                .ThenBy(e => e.Name);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<DirectoryEntry>
            {
                TotalCount = total,
                Items = items
            };
        }

        public async Task<Dictionary<int, int>> GetCategoryEntryCountsAsync()
        {
            return await this.context.DirectoryEntries
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed)
                .GroupBy(e => e.SubCategory.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
        }

        public async Task<Dictionary<int, int>> GetSubcategoryEntryCountsAsync()
        {
            return await this.context.DirectoryEntries
                .Where(de => de.DirectoryStatus != DirectoryStatus.Removed)
                .GroupBy(de => de.SubCategoryId)
                .Select(g => new
                {
                    SubCategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(g => g.SubCategoryId, g => g.Count);
        }

        public async Task<PagedResult<DirectoryEntry>> GetActiveEntriesBySubcategoryPagedAsync(
            int subCategoryId,
            int page,
            int pageSize)
        {
            var query = this.context.DirectoryEntries
                .Where(e => e.SubCategoryId == subCategoryId
                            && e.DirectoryStatus != DirectoryStatus.Removed)
                .OrderBy(e => e.Name);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<DirectoryEntry>
            {
                TotalCount = total,
                Items = items
            };
        }

        public async Task<IReadOnlyList<DirectoryEntryUrl>> GetAllIdsAndUrlsAsync()
        {
            // AsNoTracking → no change-tracking overhead
            // filter out Removed/Unknown if you only want “active”
            var inactive = new[] { DirectoryStatus.Removed, DirectoryStatus.Unknown };

            return await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(e => !inactive.Contains(e.DirectoryStatus))
                .OrderBy(e => e.DirectoryEntryId)
                .Select(e => new DirectoryEntryUrl
                {
                    DirectoryEntryId = e.DirectoryEntryId,
                    Link = e.Link
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<int> CountByCategoryAsync(int categoryId)
        {
            return await this.context.DirectoryEntries
                .Where(de => de.SubCategory.CategoryId == categoryId && de.DirectoryStatus != DirectoryStatus.Removed)
                .CountAsync();
        }

        public async Task<int> CountBySubcategoryAsync(int subCategoryId)
        {
            return await this.context.DirectoryEntries
                .Where(e => e.SubCategoryId == subCategoryId && e.DirectoryStatus != DirectoryStatus.Removed)
                .CountAsync();
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
    }
}
