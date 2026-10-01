using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class DirectoryEntryGuaranteeRepository : IDirectoryEntryGuaranteeRepository
    {
        private readonly ApplicationDbContext db;

        public DirectoryEntryGuaranteeRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<List<DirectoryEntryGuarantee>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            return await this.db.DirectoryEntryGuarantees
                .AsNoTracking()
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.DirectoryEntryGuaranteeId)
                .ToListAsync(ct);
        }

        public async Task CreateManyAsync(IEnumerable<DirectoryEntryGuarantee> models, CancellationToken ct = default)
        {
            var list = (models ?? Enumerable.Empty<DirectoryEntryGuarantee>()).ToList();
            if (list.Count == 0)
            {
                return;
            }

            await this.db.DirectoryEntryGuarantees.AddRangeAsync(list, ct);
            await this.db.SaveChangesAsync(ct);
        }

        public async Task DeleteByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            var existing = await this.db.DirectoryEntryGuarantees
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .ToListAsync(ct);

            if (existing.Count == 0)
            {
                return;
            }

            this.db.DirectoryEntryGuarantees.RemoveRange(existing);
            await this.db.SaveChangesAsync(ct);
        }
    }
}
