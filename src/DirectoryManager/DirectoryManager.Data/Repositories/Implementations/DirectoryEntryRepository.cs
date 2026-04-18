using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Utilities.Helpers;
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
        /// Prior vote count (m). New items are treated as if they already have
        /// this many reviews at the prior mean. Raise this to demand more
        /// evidence before an item escapes the prior; lower it to trust small
        /// samples more quickly.
        /// </summary>
        private const double RatingPriorCount = 10.0;

        /// <summary>
        /// Prior mean (C). The neutral score an unreviewed item is assumed to
        /// have. 3.0 is the midpoint of a 1–5 star scale.
        /// </summary>
        private const double RatingPriorMean = 3.0;

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

        public async Task<DirectoryEntry?> GetByKey(string directoryEntryKey)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.DirectoryEntryKey == directoryEntryKey)
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

        public async Task<DirectoryEntry?> GetByNameAndSubcategoryAsync(string name, int subcategoryId)
        {
            return await this.BaseQuery()
                .FirstOrDefaultAsync(de => de.Name == name && de.SubCategoryId == subcategoryId)
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
            ArgumentNullException.ThrowIfNull(entry);

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
            existing.ProofLink = entry.ProofLink;
            existing.VideoLink = entry.VideoLink;
            existing.DirectoryStatus = entry.DirectoryStatus;
            existing.Description = entry.Description;
            existing.Location = entry.Location;
            existing.Processor = entry.Processor;
            existing.Note = entry.Note;
            existing.Contact = entry.Contact;
            existing.SubCategoryId = entry.SubCategoryId;
            existing.FoundedDate = entry.FoundedDate;
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

        public async Task<List<DirectoryEntrySitemapRow>> GetSitemapEntriesAsync(CancellationToken ct = default)
        {
            return await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed)
                .Select(e => new DirectoryEntrySitemapRow
                {
                    DirectoryEntryId = e.DirectoryEntryId,
                    DirectoryEntryKey = e.DirectoryEntryKey,
                    DirectoryStatus = e.DirectoryStatus,
                    CreateDate = e.CreateDate,
                    UpdateDate = e.UpdateDate,
                    CountryCode = e.CountryCode
                })
                .ToListAsync(ct);
        }

        public async Task<PagedResult<DirectoryEntry>> SearchIncludingRemovedAsync(string query, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 50);

            query = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return new PagedResult<DirectoryEntry>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = new List<DirectoryEntry>()
                };
            }

            var normalized = NormalizeSearchTerm(query);

            var filtered = this.BuildSearchServerSideQuery(normalized, includeRemoved: true);

            var candidates = await filtered.ToListAsync().ConfigureAwait(false);

            var scored = ScoreSearchCandidates(
                    candidates,
                    normalized.term,
                    normalized.rootTerm,
                    normalized.countryCode,
                    normalized.isUrlTerm,
                    normalized.noSlash,
                    normalized.withSlash,
                    normalized.hostOnly,
                    normalized.hostNoWww,
                    normalized.termCompact)
                .ToList();

            int total = scored.Count;

            var items = scored
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Entry)
                .ToList();

            return new PagedResult<DirectoryEntry>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = items
            };
        }

        public async Task<List<CountryCountRow>> GetActiveCountryCountsForSitemapAsync(CancellationToken ct = default)
        {
            var activeStatuses = new[]
            {
                DirectoryStatus.Admitted,
                DirectoryStatus.Verified,
                DirectoryStatus.Scam,
                DirectoryStatus.Questionable
            };

            return await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(e =>
                    activeStatuses.Contains(e.DirectoryStatus) &&
                    !string.IsNullOrWhiteSpace(e.CountryCode))
                .GroupBy(e => e.CountryCode!.Trim().ToUpper())
                .Select(g => new CountryCountRow
                {
                    CountryCode = g.Key,
                    Count = g.Count()
                })
                .ToListAsync(ct);
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
            var ids = await this.context.DirectoryEntries
                .Where(x => x.DirectoryStatus != DirectoryStatus.Removed)
                .AsNoTracking()
                .OrderByDescending(e => e.CreateDate)
                .Take(count)
                .Select(e => e.DirectoryEntryId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (ids.Count == 0)
            {
                return Array.Empty<DirectoryEntry>();
            }

            var entries = await this.BaseQuery()
                .AsNoTracking()
                .Where(e => ids.Contains(e.DirectoryEntryId))
                .ToListAsync()
                .ConfigureAwait(false);

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
            var query = this.context.DirectoryEntries
                .Include(e => e.SubCategory)
                .ThenInclude(sc => sc.Category)
                .Where(e =>
                    e.DirectoryStatus != DirectoryStatus.Removed &&
                    (e.SubCategory != null &&
                    e.SubCategory.CategoryId == categoryId))
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

        public async Task<PagedResult<CountryWithCount>> ListActiveCountriesWithCountsPagedAsync(int page, int pageSize)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            var grouped = await this.context.DirectoryEntries
                .Where(e =>
                    (e.DirectoryStatus == DirectoryStatus.Verified
                  || e.DirectoryStatus == DirectoryStatus.Admitted
                  || e.DirectoryStatus == DirectoryStatus.Questionable
                  || e.DirectoryStatus == DirectoryStatus.Scam)
                         && !string.IsNullOrWhiteSpace(e.CountryCode))
                .GroupBy(e => e.CountryCode!.Trim().ToUpper())
                .Select(g => new { Code = g.Key, Count = g.Count() })
                .ToListAsync();

            if (grouped.Count == 0)
            {
                return new PagedResult<CountryWithCount> { TotalCount = 0, Items = new List<CountryWithCount>() };
            }

            var countries = CountryHelper.GetCountries();
            var items = grouped
                .Where(x => countries.ContainsKey(x.Code))
                .Select(x =>
                {
                    var name = countries[x.Code];
                    var key = StringHelpers.UrlKey(name);
                    return new CountryWithCount { Code = x.Code, Name = name, Key = key, Count = x.Count };
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .ToList();

            int total = items.Count;
            var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<CountryWithCount>
            {
                TotalCount = total,
                Items = pageItems
            };
        }

        public async Task<PagedResult<DirectoryEntry>> ListActiveEntriesByCountryPagedAsync(string countryCode, int page, int pageSize)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }

            var code = (countryCode ?? "").Trim().ToUpperInvariant();

            var q = this.context.DirectoryEntries
                .Include(e => e.SubCategory)
                .ThenInclude(sc => sc.Category)
                .Where(e =>
                    (e.DirectoryStatus == DirectoryStatus.Verified
                  || e.DirectoryStatus == DirectoryStatus.Admitted
                  || e.DirectoryStatus == DirectoryStatus.Questionable
                  || e.DirectoryStatus == DirectoryStatus.Scam)
                 && e.CountryCode != null
                 && e.CountryCode.ToUpper() == code)
                .OrderBy(e => e.Name);

            var total = await q.CountAsync();
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<DirectoryEntry> { TotalCount = total, Items = items };
        }

        public async Task<List<IdNameOption>> ListCategoryOptionsAsync()
        {
            var cats = await this.BaseQuery()
                .AsNoTracking()
                .Where(e => e.SubCategory != null && e.SubCategory.Category != null)
                .Select(e => new { e.SubCategory!.Category!.CategoryId, e.SubCategory.Category.Name })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            return cats.Select(x => new IdNameOption { Id = x.CategoryId, Name = x.Name }).ToList();
        }

        public async Task<List<IdNameOption>> ListSubCategoryOptionsAsync(int categoryId)
        {
            var subs = await this.BaseQuery()
                .AsNoTracking()
                .Where(e => e.SubCategory != null && e.SubCategory.CategoryId == categoryId)
                .Select(e => new { e.SubCategory!.SubCategoryId, e.SubCategory.Name })
                .Distinct()
                .OrderBy(x => x.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            return subs.Select(x => new IdNameOption { Id = x.SubCategoryId, Name = x.Name }).ToList();
        }

        public async Task<Dictionary<int, DirectoryEntry>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var list = (ids ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (list.Count == 0)
            {
                return new Dictionary<int, DirectoryEntry>();
            }

            var rows = await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(d => list.Contains(d.DirectoryEntryId))
                .Select(d => new DirectoryEntry
                {
                    DirectoryEntryKey = d.DirectoryEntryKey,
                    DirectoryEntryId = d.DirectoryEntryId,
                    Name = d.Name,
                    Link = d.Link,
                    SubCategoryId = d.SubCategoryId,
                    SubCategory = d.SubCategory == null ? null : new Subcategory
                    {
                        SubCategoryId = d.SubCategory.SubCategoryId,
                        CategoryId = d.SubCategory.CategoryId,
                        Name = d.SubCategory.Name,
                        SubCategoryKey = d.SubCategory.SubCategoryKey,
                        Category = d.SubCategory.Category == null ? null : new Category
                        {
                            CategoryId = d.SubCategory.Category.CategoryId,
                            Name = d.SubCategory.Category.Name,
                            CategoryKey = d.SubCategory.Category.CategoryKey
                        }
                    }
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.ToDictionary(x => x.DirectoryEntryId, x => x);
        }

        public async Task<PagedResult<DirectoryEntry>> SearchAsync(string term, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new PagedResult<DirectoryEntry>();
            }

            var normalized = NormalizeSearchTerm(term);
            var filtered = this.BuildSearchServerSideQuery(normalized, false);
            var candidates = await filtered.ToListAsync().ConfigureAwait(false);

            var scored = ScoreSearchCandidates(
                    candidates,
                    normalized.term,
                    normalized.rootTerm,
                    normalized.countryCode,
                    normalized.isUrlTerm,
                    normalized.noSlash,
                    normalized.withSlash,
                    normalized.hostOnly,
                    normalized.hostNoWww,
                    normalized.termCompact)
                .ToList();

            int total = scored.Count;
            var items = scored
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.Entry)
                .ToList();

            return new PagedResult<DirectoryEntry> { TotalCount = total, Items = items };
        }

        public async Task<List<string>> ListActiveCountryCodesAsync(CancellationToken ct = default)
        {
            return await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(e =>
                    (e.DirectoryStatus == DirectoryStatus.Verified
                  || e.DirectoryStatus == DirectoryStatus.Admitted
                  || e.DirectoryStatus == DirectoryStatus.Questionable
                  || e.DirectoryStatus == DirectoryStatus.Scam)
                    && !string.IsNullOrWhiteSpace(e.CountryCode))
                .Select(e => e.CountryCode!.Trim().ToUpper())
                .Distinct()
                .OrderBy(code => code)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }

        public async Task<PagedResult<DirectoryEntry>> SearchNonRemovedAsync(string query, int page, int pageSize)
        {
            if (TryParseAuthorOnlyQuery(query, out var authorTerm))
            {
                return await this.SearchByAuthorPostsAsync(authorTerm, page, pageSize, includeRemoved: false)
                    .ConfigureAwait(false);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 50);

            query = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return new PagedResult<DirectoryEntry>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = new List<DirectoryEntry>()
                };
            }

            var result = await this.SearchAsync(query, page, pageSize).ConfigureAwait(false);
            result.Page = page;
            result.PageSize = pageSize;

            if (result.TotalCount == 0)
            {
                var authorFallback = await this.SearchByAuthorPostsAsync(
                    authorQuery: query,
                    page: page,
                    pageSize: pageSize,
                    includeRemoved: false,
                    ct: CancellationToken.None).ConfigureAwait(false);

                if (authorFallback.TotalCount > 0)
                {
                    return authorFallback;
                }
            }

            return result;
        }

        public async Task<PagedResult<DirectoryEntry>> FilterAsync(DirectoryFilterQuery q)
        {
            q ??= new DirectoryFilterQuery();

            int page = q.Page < 1 ? 1 : q.Page;
            int pageSize = q.PageSize < 1 ? 10 : q.PageSize;

            var statuses = GetStatusesOrDefault(q);
            var baseQ = this.BuildFilterBaseQuery(q, statuses);

            var pageIdsQ = this.BuildFilterPageIdsQuery(baseQ, q.Sort);

            int total = await this.GetFilterTotalAsync(baseQ, q.Sort).ConfigureAwait(false);

            var pageIds = await pageIdsQ
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);

            if (pageIds.Count == 0)
            {
                return new PagedResult<DirectoryEntry> { TotalCount = total, Items = new List<DirectoryEntry>() };
            }

            var items = await this.LoadEntriesByIdsPreservingOrderAsync(pageIds).ConfigureAwait(false);

            return new PagedResult<DirectoryEntry>
            {
                TotalCount = total,
                Items = items
            };
        }

        public async Task<PagedResult<DirectoryEntry>> SearchByAuthorPostsAsync(
            string authorQuery,
            int page,
            int pageSize,
            bool includeRemoved = false,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 50);

            authorQuery = (authorQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(authorQuery))
            {
                return new PagedResult<DirectoryEntry>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = new List<DirectoryEntry>()
                };
            }

            var n = NormalizeAuthorSearch(authorQuery);
            IQueryable<DirectoryEntryReview> ReviewAuthorFilter(IQueryable<DirectoryEntryReview> q) =>
            q.Where(r =>
                r.AuthorFingerprint != null && r.AuthorFingerprint != "" &&
                (
                    EF.Functions.Like(r.AuthorFingerprint, n.pattern) ||
                    (n.rootPattern != null && EF.Functions.Like(r.AuthorFingerprint, n.rootPattern)) ||
                    (n.compactPattern != null && EF.Functions.Like(r.AuthorFingerprint.Replace(" ", ""), n.compactPattern))));

            IQueryable<DirectoryEntryReviewComment> ReplyAuthorFilter(IQueryable<DirectoryEntryReviewComment> q) =>
                q.Where(c =>
                    c.AuthorFingerprint != null && c.AuthorFingerprint != "" &&
                    (
                        EF.Functions.Like(c.AuthorFingerprint, n.pattern) ||
                        (n.rootPattern != null && EF.Functions.Like(c.AuthorFingerprint, n.rootPattern)) ||
                        (n.compactPattern != null && EF.Functions.Like(c.AuthorFingerprint.Replace(" ", ""), n.compactPattern))));

            var entryFilter = this.context.DirectoryEntries.AsNoTracking().AsQueryable();
            if (!includeRemoved)
            {
                entryFilter = entryFilter.Where(e => e.DirectoryStatus != DirectoryStatus.Removed);
            }

            var reviewsActivity =
                from r in ReviewAuthorFilter(this.context.DirectoryEntryReviews.AsNoTracking())
                join e in entryFilter on r.DirectoryEntryId equals e.DirectoryEntryId
                where r.ModerationStatus == ReviewModerationStatus.Approved
                group r by r.DirectoryEntryId into g
                select new { EntryId = g.Key, Last = g.Max(x => x.CreateDate) };

            var repliesActivity =
                from c in ReplyAuthorFilter(this.context.DirectoryEntryReviewComments.AsNoTracking())
                join r in this.context.DirectoryEntryReviews.AsNoTracking()
                    on c.DirectoryEntryReviewId equals r.DirectoryEntryReviewId
                join e in entryFilter on r.DirectoryEntryId equals e.DirectoryEntryId
                where c.ModerationStatus == ReviewModerationStatus.Approved
                      && r.ModerationStatus == ReviewModerationStatus.Approved
                group c by r.DirectoryEntryId into g
                select new { EntryId = g.Key, Last = g.Max(x => x.UpdateDate ?? x.CreateDate) };

            var merged =
                from x in reviewsActivity.Concat(repliesActivity)
                group x by x.EntryId into g
                select new { EntryId = g.Key, Last = g.Max(z => z.Last) };

            int total = await merged.CountAsync(ct).ConfigureAwait(false);
            if (total == 0)
            {
                return new PagedResult<DirectoryEntry>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = new List<DirectoryEntry>()
                };
            }

            var pageIds = await merged
                .OrderByDescending(x => x.Last)
                .ThenByDescending(x => x.EntryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.EntryId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var items = await this.LoadEntriesByIdsPreservingOrderAsync(pageIds).ConfigureAwait(false);

            return new PagedResult<DirectoryEntry>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = items
            };
        }

        private IQueryable<DirectoryEntry> BuildSearchServerSideQuery(SearchTermInfo n, bool includeRemoved)
        {
            return this.BaseQuery()
                .Where(e =>
                    (includeRemoved || e.DirectoryStatus != DirectoryStatus.Removed) &&
                    (
                        (n.countryCode != null && e.CountryCode == n.countryCode)

                        || EF.Functions.Like(e.Name.ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like(e.Name.ToLower(), n.rootPattern))
                        || (n.compactPattern != null &&
                            EF.Functions.Like(
                                e.Name.ToLower()
                                      .Replace("-", "")
                                      .Replace(".", "")
                                      .Replace(" ", "")
                                      .Replace("_", ""),
                                n.compactPattern))

                        || EF.Functions.Like((e.Description ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Description ?? "").ToLower(), n.rootPattern))

                        || EF.Functions.Like(e.SubCategory!.Name.ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like(e.SubCategory.Name.ToLower(), n.rootPattern))
                        || EF.Functions.Like(e.SubCategory.Category!.Name.ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like(e.SubCategory.Category.Name.ToLower(), n.rootPattern))

                        || e.EntryTags.Any(et =>
                            EF.Functions.Like(et.Tag.Name.ToLower(), n.primaryPattern) ||
                            (n.rootPattern != null && EF.Functions.Like(et.Tag.Name.ToLower(), n.rootPattern)))

                        || EF.Functions.Like((e.Note ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Note ?? "").ToLower(), n.rootPattern))
                        || EF.Functions.Like((e.Processor ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Processor ?? "").ToLower(), n.rootPattern))
                        || EF.Functions.Like((e.Location ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Location ?? "").ToLower(), n.rootPattern))
                        || EF.Functions.Like((e.Contact ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Contact ?? "").ToLower(), n.rootPattern))

                        || EF.Functions.Like((e.Link ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Link ?? "").ToLower(), n.rootPattern))
                        || (n.compactPattern != null &&
                            EF.Functions.Like(
                                (e.Link ?? "").ToLower()
                                              .Replace("-", "")
                                              .Replace(".", "")
                                              .Replace(" ", "")
                                              .Replace("_", ""),
                                n.compactPattern))

                        || EF.Functions.Like((e.Link2 ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Link2 ?? "").ToLower(), n.rootPattern))
                        || (n.compactPattern != null &&
                            EF.Functions.Like(
                                (e.Link2 ?? "").ToLower()
                                               .Replace("-", "")
                                               .Replace(".", "")
                                               .Replace(" ", "")
                                               .Replace("_", ""),
                                n.compactPattern))

                        || EF.Functions.Like((e.Link3 ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.Link3 ?? "").ToLower(), n.rootPattern))
                        || (n.compactPattern != null &&
                            EF.Functions.Like(
                                (e.Link3 ?? "").ToLower()
                                               .Replace("-", "")
                                               .Replace(".", "")
                                               .Replace(" ", "")
                                               .Replace("_", ""),
                                n.compactPattern))

                        || EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.primaryPattern)
                        || (n.rootPattern != null && EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.rootPattern))
                        || (n.compactPattern != null &&
                            EF.Functions.Like(
                                (e.ProofLink ?? "").ToLower()
                                                  .Replace("-", "")
                                                  .Replace(".", "")
                                                  .Replace(" ", "")
                                                  .Replace("_", ""),
                                n.compactPattern))

                        || (n.isUrlTerm && (
                               EF.Functions.Like((e.Link ?? "").ToLower(), n.pNoSlash) ||
                               EF.Functions.Like((e.Link ?? "").ToLower(), n.pWithSlash) ||
                               (n.pHostOnly != "" && EF.Functions.Like((e.Link ?? "").ToLower(), n.pHostOnly)) ||
                               (n.pNoWww != "" && EF.Functions.Like((e.Link ?? "").ToLower(), n.pNoWww)) ||

                               EF.Functions.Like((e.Link2 ?? "").ToLower(), n.pNoSlash) ||
                               EF.Functions.Like((e.Link2 ?? "").ToLower(), n.pWithSlash) ||
                               (n.pHostOnly != "" && EF.Functions.Like((e.Link2 ?? "").ToLower(), n.pHostOnly)) ||
                               (n.pNoWww != "" && EF.Functions.Like((e.Link2 ?? "").ToLower(), n.pNoWww)) ||

                               EF.Functions.Like((e.Link3 ?? "").ToLower(), n.pNoSlash) ||
                               EF.Functions.Like((e.Link3 ?? "").ToLower(), n.pWithSlash) ||
                               (n.pHostOnly != "" && EF.Functions.Like((e.Link3 ?? "").ToLower(), n.pHostOnly)) ||
                               (n.pNoWww != "" && EF.Functions.Like((e.Link3 ?? "").ToLower(), n.pNoWww)) ||

                               EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.pNoSlash) ||
                               EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.pWithSlash) ||
                               (n.pHostOnly != "" && EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.pHostOnly)) ||
                               (n.pNoWww != "" && EF.Functions.Like((e.ProofLink ?? "").ToLower(), n.pNoWww))))));
        }

        private sealed record AuthorSearchTerm(
            string term,
            string pattern,
            string? rootPattern,
            string compact,
            string? compactPattern);

        private static AuthorSearchTerm NormalizeAuthorSearch(string q)
        {
            var term = (q ?? string.Empty).Trim().ToLowerInvariant();
            var pattern = $"%{term}%";

            string? rootPattern = null;
            if (term.EndsWith("s") && term.Length > 3)
            {
                var root = term[..^1];
                rootPattern = $"%{root}%";
            }

            var compact = new string(term.Where(char.IsLetterOrDigit).ToArray());
            string? compactPattern = string.IsNullOrEmpty(compact) ? null : $"%{compact}%";

            return new AuthorSearchTerm(term, pattern, rootPattern, compact, compactPattern);
        }

        private static bool TryParseAuthorOnlyQuery(string q, out string authorTerm)
        {
            authorTerm = string.Empty;
            q = (q ?? string.Empty).Trim();

            if (q.StartsWith("author:", StringComparison.OrdinalIgnoreCase) ||
                q.StartsWith("by:", StringComparison.OrdinalIgnoreCase) ||
                q.StartsWith("user:", StringComparison.OrdinalIgnoreCase) ||
                q.StartsWith("fp:", StringComparison.OrdinalIgnoreCase))
            {
                var idx = q.IndexOf(':');
                authorTerm = idx >= 0 ? q[(idx + 1)..].Trim() : string.Empty;
                return !string.IsNullOrWhiteSpace(authorTerm);
            }

            if (q.StartsWith("@", StringComparison.Ordinal))
            {
                authorTerm = q[1..].Trim();
                return !string.IsNullOrWhiteSpace(authorTerm);
            }

            return false;
        }

        private sealed record SearchTermInfo(
            string term,
            string? countryCode,
            string? rootTerm,
            string primaryPattern,
            string? rootPattern,
            string termCompact,
            string? compactPattern,
            bool isUrlTerm,
            string noSlash,
            string withSlash,
            string hostOnly,
            string hostNoWww,
            string pNoSlash,
            string pWithSlash,
            string pHostOnly,
            string pNoWww);

        private static SearchTermInfo NormalizeSearchTerm(string term)
        {
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

            string termCompact = new string(term.Where(char.IsLetterOrDigit).ToArray());
            string? compactPattern = string.IsNullOrEmpty(termCompact) ? null : $"%{termCompact}%";

            static bool LooksLikeUrl(string s) =>
                s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

            static string TrimTrailingSlash(string s) => s.EndsWith("/") ? s[..^1] : s;

            static string ExtractHost(string url)
            {
                try
                {
                    if (!url.Contains("://", StringComparison.Ordinal))
                    {
                        url = "https://" + url;
                    }

                    var u = new Uri(url, UriKind.Absolute);
                    return (u.Host ?? "").ToLowerInvariant();
                }
                catch
                {
                    var s = url;
                    s = s.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
                    int slash = s.IndexOf('/');
                    return (slash >= 0 ? s[..slash] : s).ToLowerInvariant();
                }
            }

            static string StripWww(string host) =>
                host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

            bool isUrlTerm = LooksLikeUrl(term);
            string noSlash = isUrlTerm ? TrimTrailingSlash(term) : term;
            string withSlash = isUrlTerm ? (noSlash + "/") : term;

            string hostOnly = isUrlTerm ? ExtractHost(term) : string.Empty;
            string hostNoWww = isUrlTerm ? StripWww(hostOnly) : string.Empty;

            string pNoSlash = $"%{noSlash}%";
            string pWithSlash = $"%{withSlash}%";
            string pHostOnly = string.IsNullOrEmpty(hostOnly) ? "" : $"%{hostOnly}%";
            string pNoWww = string.IsNullOrEmpty(hostNoWww) ? "" : $"%{hostNoWww}%";

            return new SearchTermInfo(
                term: term,
                countryCode: countryCode,
                rootTerm: rootTerm,
                primaryPattern: primaryPattern,
                rootPattern: rootPattern,
                termCompact: termCompact,
                compactPattern: compactPattern,
                isUrlTerm: isUrlTerm,
                noSlash: noSlash,
                withSlash: withSlash,
                hostOnly: hostOnly,
                hostNoWww: hostNoWww,
                pNoSlash: pNoSlash,
                pWithSlash: pWithSlash,
                pHostOnly: pHostOnly,
                pNoWww: pNoWww);
        }

        private static IEnumerable<(DirectoryEntry Entry, int Score, int Hits, int Weight, bool CountryMatch)> ScoreSearchCandidates(
            List<DirectoryEntry> candidates,
            string term,
            string? rootTerm,
            string? countryCode,
            bool isUrlTerm,
            string noSlash,
            string withSlash,
            string hostOnly,
            string hostNoWww,
            string termCompact)
        {
            static int CountOcc(string? field, string t)
            {
                if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(t))
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

            static string NormalizeToken(string? s)
            {
                if (string.IsNullOrEmpty(s))
                {
                    return string.Empty;
                }

                var chars = s.ToLowerInvariant()
                             .Where(char.IsLetterOrDigit)
                             .ToArray();
                return new string(chars);
            }

            const int CountryBoost = 7;

            string termCompactForScore = termCompact;

            return candidates
                .Select(e =>
                {
                    string nameNorm = NormalizeToken(e.Name);
                    string linkNorm = NormalizeToken(e.Link);
                    string link2Norm = NormalizeToken(e.Link2);
                    string link3Norm = NormalizeToken(e.Link3);
                    string proofNorm = NormalizeToken(e.ProofLink);

                    int hits =
                        CountOcc(e.Name, term) + (rootTerm != null ? CountOcc(e.Name, rootTerm) : 0) +
                        CountOcc(e.Description, term) + (rootTerm != null ? CountOcc(e.Description, rootTerm) : 0) +
                        CountOcc(e.SubCategory?.Name, term) + (rootTerm != null ? CountOcc(e.SubCategory?.Name, rootTerm) : 0) +
                        CountOcc(e.SubCategory?.Category?.Name, term) + (rootTerm != null ? CountOcc(e.SubCategory?.Category?.Name, rootTerm) : 0) +
                        e.EntryTags.Sum(et => CountOcc(et.Tag.Name, term) + (rootTerm != null ? CountOcc(et.Tag.Name, rootTerm) : 0)) +
                        CountOcc(e.Note, term) + (rootTerm != null ? CountOcc(e.Note, rootTerm) : 0) +
                        CountOcc(e.Processor, term) + (rootTerm != null ? CountOcc(e.Processor, rootTerm) : 0) +
                        CountOcc(e.Location, term) + (rootTerm != null ? CountOcc(e.Location, rootTerm) : 0) +
                        CountOcc(e.Contact, term) + (rootTerm != null ? CountOcc(e.Contact, rootTerm) : 0) +
                        CountOcc(e.Link, term) + (rootTerm != null ? CountOcc(e.Link, rootTerm) : 0) +
                        CountOcc(e.Link2, term) + (rootTerm != null ? CountOcc(e.Link2, rootTerm) : 0) +
                        CountOcc(e.Link3, term) + (rootTerm != null ? CountOcc(e.Link3, rootTerm) : 0) +
                        CountOcc(e.ProofLink, term) + (rootTerm != null ? CountOcc(e.ProofLink, rootTerm) : 0) +

                        (!string.IsNullOrEmpty(termCompactForScore)
                            ? CountOcc(nameNorm, termCompactForScore)
                              + CountOcc(linkNorm, termCompactForScore)
                              + CountOcc(link2Norm, termCompactForScore)
                              + CountOcc(link3Norm, termCompactForScore)
                              + CountOcc(proofNorm, termCompactForScore)
                            : 0) +

                        (isUrlTerm
                            ? CountOcc(e.Link, noSlash) + CountOcc(e.Link, withSlash)
                             + CountOcc(e.Link2, noSlash) + CountOcc(e.Link2, withSlash)
                             + CountOcc(e.Link3, noSlash) + CountOcc(e.Link3, withSlash)
                             + CountOcc(e.ProofLink, noSlash) + CountOcc(e.ProofLink, withSlash)
                             + (string.IsNullOrEmpty(hostOnly) ? 0 :
                                    CountOcc(e.Link, hostOnly) + CountOcc(e.Link2, hostOnly) +
                                    CountOcc(e.Link3, hostOnly) + CountOcc(e.ProofLink, hostOnly))
                             + (string.IsNullOrEmpty(hostNoWww) ? 0 :
                                    CountOcc(e.Link, hostNoWww) + CountOcc(e.Link2, hostNoWww) +
                                    CountOcc(e.Link3, hostNoWww) + CountOcc(e.ProofLink, hostNoWww))
                            : 0);

                    bool countryMatch = countryCode != null &&
                                        string.Equals(e.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase);

                    int weight = e.DirectoryStatus switch
                    {
                        DirectoryStatus.Verified => 4,
                        DirectoryStatus.Admitted => 3,
                        DirectoryStatus.Questionable => 2,
                        DirectoryStatus.Scam => 1,
                        _ => 0,
                    };

                    int score = hits + (countryMatch ? CountryBoost : 0);

                    return (Entry: e, Score: score, Hits: hits, Weight: weight, CountryMatch: countryMatch);
                })
                .Where(x => x.Hits > 0 || x.CountryMatch)
                .OrderByDescending(x => x.Weight)
                .ThenByDescending(x => x.Score);
        }

        private static List<DirectoryStatus> GetStatusesOrDefault(DirectoryFilterQuery q)
        {
            return (q.Statuses is { Count: > 0 })
                ? q.Statuses.Distinct().ToList()
                : new List<DirectoryStatus> { DirectoryStatus.Admitted, DirectoryStatus.Verified };
        }

        private IQueryable<DirectoryEntry> BuildFilterBaseQuery(DirectoryFilterQuery q, List<DirectoryStatus> statuses)
        {
            var baseQ = this.context.DirectoryEntries.AsNoTracking().AsQueryable();

            // Status filter
            baseQ = baseQ.Where(e => statuses.Contains(e.DirectoryStatus));

            // Country filter
            if (!string.IsNullOrWhiteSpace(q.Country))
            {
                var code = q.Country.Trim().ToUpperInvariant();
                baseQ = baseQ.Where(e => e.CountryCode != null && e.CountryCode.ToUpper() == code);
            }

            // Has Video
            if (q.HasVideo)
            {
                baseQ = baseQ.Where(e => !string.IsNullOrWhiteSpace(e.VideoLink));
            }

            // Has Tor (.onion)
            if (q.HasTor)
            {
                const string onionExtension = ".onion";
                baseQ = baseQ.Where(e =>
                    (e.Link2 ?? "").Contains(onionExtension) ||
                    (e.Link3 ?? "").Contains(onionExtension));
            }

            // Has i2p (.i2p)
            if (q.HasI2p)
            {
                const string i2pExtension = ".i2p";
                baseQ = baseQ.Where(e =>
                    (e.Link2 ?? "").Contains(i2pExtension) ||
                    (e.Link3 ?? "").Contains(i2pExtension));
            }

            // Category/Subcategory
            if (q.CategoryId is > 0)
            {
                int catId = q.CategoryId.Value;
                baseQ = baseQ.Where(e => e.SubCategory != null && e.SubCategory.CategoryId == catId);

                if (q.SubCategoryId is > 0)
                {
                    int subId = q.SubCategoryId.Value;
                    baseQ = baseQ.Where(e => e.SubCategoryId == subId);
                }
            }

            // Tags: must include ALL selected tags
            ApplyTagFilter(ref baseQ, q);

            return baseQ;
        }

        private static void ApplyTagFilter(ref IQueryable<DirectoryEntry> baseQ, DirectoryFilterQuery q)
        {
            if (q.TagIds is not { Count: > 0 })
            {
                return;
            }

            var tagIds = q.TagIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (tagIds.Count == 0)
            {
                return;
            }

            baseQ = baseQ.Where(e =>
                e.EntryTags
                 .Where(et => tagIds.Contains(et.TagId))
                 .Select(et => et.TagId)
                 .Distinct()
                 .Count() == tagIds.Count);
        }

        /// <summary>
        /// Builds the rated aggregate query using a Bayesian weighted average:
        ///   BayesianScore = (Σ ratings + m × C) / (count + m)
        /// where m = <see cref="RatingPriorCount"/> and C = <see cref="RatingPriorMean"/>.
        /// This prevents a single 5★ review from outranking items with many high ratings.
        /// </summary>
        private IQueryable<RatedAggRow> BuildRatedAggregate(IQueryable<DirectoryEntry> baseQ)
        {
            var approvedRatings = this.context.DirectoryEntryReviews
                .AsNoTracking()
                .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved && r.Rating.HasValue);

            return
                from e in baseQ
                join r in approvedRatings on e.DirectoryEntryId equals r.DirectoryEntryId
                group r by new { e.DirectoryEntryId, e.CreateDate } into g
                select new RatedAggRow
                {
                    DirectoryEntryId = g.Key.DirectoryEntryId,
                    CreateDate = g.Key.CreateDate,
                    AvgRating = g.Average(x => x.Rating!.Value),
                    ReviewCount = g.Count(),

                    // Bayesian weighted average — computed in SQL by EF Core.
                    // Pulls low-review-count items toward the prior mean (3.0),
                    // so volume of evidence is required to achieve a high rank.
                    BayesianScore =
                        (g.Sum(x => (double)x.Rating!.Value) + (RatingPriorCount * RatingPriorMean))
                        / (g.Count() + RatingPriorCount)
                };
        }

        private IQueryable<int> BuildFilterPageIdsQuery(IQueryable<DirectoryEntry> baseQ, DirectoryFilterSort sort)
        {
            if (sort == DirectoryFilterSort.Newest)
            {
                return baseQ
                    .OrderByDescending(e => e.CreateDate)
                    .ThenByDescending(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.Oldest)
            {
                return baseQ
                    .OrderBy(e => e.CreateDate)
                    .ThenBy(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.FoundedDateNewest)
            {
                return baseQ
                    .OrderByDescending(e => e.FoundedDate.HasValue)
                    .ThenByDescending(e => e.FoundedDate)
                    .ThenByDescending(e => e.CreateDate)
                    .ThenByDescending(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.FoundedDateOldest)
            {
                return baseQ
                    .OrderByDescending(e => e.FoundedDate.HasValue)
                    .ThenBy(e => e.FoundedDate)
                    .ThenBy(e => e.CreateDate)
                    .ThenBy(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.NameAsc)
            {
                return baseQ
                    .OrderBy(e => e.Name.ToLower())
                    .ThenBy(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.NameDesc)
            {
                return baseQ
                    .OrderByDescending(e => e.Name.ToLower())
                    .ThenByDescending(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.RecentlyUpdated)
            {
                return baseQ
                    .OrderByDescending(e => e.UpdateDate.HasValue)
                    .ThenByDescending(e => e.UpdateDate)
                    .ThenByDescending(e => e.CreateDate)
                    .ThenByDescending(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            if (sort == DirectoryFilterSort.LeastRecentlyUpdated)
            {
                return baseQ
                    .OrderByDescending(e => e.UpdateDate.HasValue)
                    .ThenBy(e => e.UpdateDate)
                    .ThenBy(e => e.CreateDate)
                    .ThenBy(e => e.DirectoryEntryId)
                    .Select(e => e.DirectoryEntryId);
            }

            // Rating sorts — ordered by Bayesian weighted average score
            IQueryable<RatedAggRow> ratedAgg = this.BuildRatedAggregate(baseQ);

            if (sort == DirectoryFilterSort.HighestRating)
            {
                return ratedAgg
                    .OrderByDescending(x => x.BayesianScore)  // weighted: volume of evidence matters
                    .ThenByDescending(x => x.ReviewCount)     // more reviews wins ties
                    .ThenByDescending(x => x.AvgRating)       // raw average as final tie-break
                    .ThenByDescending(x => x.CreateDate)
                    .ThenByDescending(x => x.DirectoryEntryId)
                    .Select(x => x.DirectoryEntryId);
            }

            // LowestRating
            return ratedAgg
                .OrderBy(x => x.BayesianScore)                // weighted
                .ThenByDescending(x => x.ReviewCount)         // more reviews wins ties
                .ThenBy(x => x.AvgRating)                     // raw average as final tie-break
                .ThenBy(x => x.CreateDate)
                .ThenBy(x => x.DirectoryEntryId)
                .Select(x => x.DirectoryEntryId);
        }

        private sealed class RatedAggRow
        {
            public int DirectoryEntryId { get; init; }
            public DateTime CreateDate { get; init; }
            public double AvgRating { get; init; }
            public int ReviewCount { get; init; }
            public double BayesianScore { get; init; }
        }

        private async Task<int> GetFilterTotalAsync(IQueryable<DirectoryEntry> baseQ, DirectoryFilterSort sort)
        {
            if (sort is DirectoryFilterSort.HighestRating or DirectoryFilterSort.LowestRating)
            {
                var approvedRatings = this.context.DirectoryEntryReviews
                    .AsNoTracking()
                    .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved && r.Rating.HasValue);

                return await (from e in baseQ
                              join r in approvedRatings on e.DirectoryEntryId equals r.DirectoryEntryId
                              select e.DirectoryEntryId)
                    .Distinct()
                    .CountAsync()
                    .ConfigureAwait(false);
            }

            return await baseQ.CountAsync().ConfigureAwait(false);
        }

        private async Task<List<DirectoryEntry>> LoadEntriesByIdsPreservingOrderAsync(List<int> pageIds)
        {
            var items = await this.BaseQuery()
                .AsNoTracking()
                .Where(e => pageIds.Contains(e.DirectoryEntryId))
                .ToListAsync()
                .ConfigureAwait(false);

            var order = pageIds
                .Select((id, idx) => new { id, idx })
                .ToDictionary(x => x.id, x => x.idx);

            return items
                .OrderBy(e => order[e.DirectoryEntryId])
                .ToList();
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
        private List<GroupedDirectoryEntry> GroupByDate(List<DirectoryEntry> list)
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