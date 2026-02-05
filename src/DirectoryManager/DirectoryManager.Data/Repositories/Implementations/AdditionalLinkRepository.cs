using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class AdditionalLinkRepository : IAdditionalLinkRepository
    {
        private readonly ApplicationDbContext db;

        public AdditionalLinkRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<List<AdditionalLink>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            return await this.db.AdditionalLinks
                .AsNoTracking()
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.AdditionalLinkId)
                .ToListAsync(ct);
        }

        public async Task<AdditionalLink> CreateAsync(AdditionalLink model, CancellationToken ct = default)
        {
            this.db.AdditionalLinks.Add(model);
            await this.db.SaveChangesAsync(ct);
            return model;
        }

        public async Task DeleteAsync(int additionalLinkId, CancellationToken ct = default)
        {
            var existing = await this.db.AdditionalLinks
                .FirstOrDefaultAsync(x => x.AdditionalLinkId == additionalLinkId, ct);

            if (existing == null)
            {
                return;
            }

            this.db.AdditionalLinks.Remove(existing);
            await this.db.SaveChangesAsync(ct);
        }

        public async Task DeleteByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var existing = await this.db.AdditionalLinks
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .ToListAsync(ct);

            if (existing.Count == 0)
            {
                return;
            }

            this.db.AdditionalLinks.RemoveRange(existing);
            await this.db.SaveChangesAsync(ct);
        }
    }
}
