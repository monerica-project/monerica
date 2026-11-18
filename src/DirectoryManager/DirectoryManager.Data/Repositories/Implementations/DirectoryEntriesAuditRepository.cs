using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.TransferModels;
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

        public async Task<(List<AuditStatusRow> Audits, List<IncidentListingRow> Listings)> GetOpenIssueHistoryAsync(
            CancellationToken ct = default)
        {
            // Listings whose CURRENT status is Scam or Questionable (an open issue).
            var listings = await this.context.DirectoryEntries
                .AsNoTracking()
                .Where(e => e.DirectoryStatus == DirectoryStatus.Scam || e.DirectoryStatus == DirectoryStatus.Questionable)
                .Select(e => new IncidentListingRow
                {
                    DirectoryEntryId = e.DirectoryEntryId,
                    Name = e.Name,
                    DirectoryEntryKey = e.DirectoryEntryKey,
                    Status = e.DirectoryStatus,
                    Note = e.Note,
                    Link = e.Link,
                    CreateDate = e.CreateDate,
                    UpdateDate = e.UpdateDate,
                })
                .ToListAsync(ct);

            if (listings.Count == 0)
            {
                return (new List<AuditStatusRow>(), listings);
            }

            var ids = listings.Select(l => l.DirectoryEntryId).ToList();
            var audits = await this.context.DirectoryEntriesAudit
                .AsNoTracking()
                .Where(a => ids.Contains(a.DirectoryEntryId))
                .Select(a => new AuditStatusRow
                {
                    AuditId = a.DirectoryEntriesAuditId,
                    DirectoryEntryId = a.DirectoryEntryId,
                    Status = a.DirectoryStatus,
                    EffectiveUtc = a.UpdateDate ?? a.CreateDate,
                    Note = a.Note,
                })
                .ToListAsync(ct);

            return (audits, listings);
        }
    }
}