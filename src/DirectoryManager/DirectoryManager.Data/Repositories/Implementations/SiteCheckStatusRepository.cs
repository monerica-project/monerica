using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class SiteCheckStatusRepository : ISiteCheckStatusRepository
    {
        private readonly ApplicationDbContext db;

        public SiteCheckStatusRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<SiteCheckStatus?> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            return await this.db.SiteCheckStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DirectoryEntryId == directoryEntryId, ct);
        }

        public async Task UpsertAsync(SiteCheckStatus model, CancellationToken ct = default)
        {
            var existing = await this.db.SiteCheckStatuses
                .FirstOrDefaultAsync(x => x.DirectoryEntryId == model.DirectoryEntryId, ct);

            if (existing == null)
            {
                model.CreateDate = DateTime.UtcNow;
                this.db.SiteCheckStatuses.Add(model);
            }
            else
            {
                existing.ClearnetFailStreak = model.ClearnetFailStreak;
                existing.OnionFailStreak = model.OnionFailStreak;
                existing.LastCheckedUtc = model.LastCheckedUtc;
                existing.UpdateDate = DateTime.UtcNow;
            }

            await this.db.SaveChangesAsync(ct);
        }
    }
}
