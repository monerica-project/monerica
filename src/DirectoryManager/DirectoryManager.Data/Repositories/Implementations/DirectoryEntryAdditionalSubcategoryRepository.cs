using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class DirectoryEntryAdditionalSubcategoryRepository : IDirectoryEntryAdditionalSubcategoryRepository
    {
        private readonly ApplicationDbContext db;

        public DirectoryEntryAdditionalSubcategoryRepository(ApplicationDbContext db)
        {
            this.db = db;
        }

        public async Task<List<DirectoryEntryAdditionalSubcategory>> GetByDirectoryEntryIdAsync(int directoryEntryId, CancellationToken ct = default)
        {
            return await this.db.DirectoryEntryAdditionalSubcategories
                .AsNoTracking()
                .Include(a => a.SubCategory).ThenInclude(sc => sc.Category)
                .Where(a => a.DirectoryEntryId == directoryEntryId)
                .OrderBy(a => a.DirectoryEntryAdditionalSubcategoryId)
                .ToListAsync(ct);
        }

        public async Task RemoveAsync(int directoryEntryId, int subCategoryId, CancellationToken ct = default)
        {
            var rows = await this.db.DirectoryEntryAdditionalSubcategories
                .Where(a => a.DirectoryEntryId == directoryEntryId && a.SubCategoryId == subCategoryId)
                .ToListAsync(ct);
            if (rows.Count == 0)
            {
                return;
            }

            this.db.DirectoryEntryAdditionalSubcategories.RemoveRange(rows);
            await this.db.SaveChangesAsync(ct);
        }

        public async Task SyncAsync(int directoryEntryId, int mainSubCategoryId, IEnumerable<DirectoryEntryAdditionalSubcategory> rows, CancellationToken ct = default)
        {
            var wanted = (rows ?? Enumerable.Empty<DirectoryEntryAdditionalSubcategory>())
                .Where(r => r.SubCategoryId > 0 && r.SubCategoryId != mainSubCategoryId)
                .GroupBy(r => r.SubCategoryId)
                .Select(g => g.First())
                .Take(IntegerConstants.MaxAdditionalSubcategories)
                .ToList();

            var existing = await this.db.DirectoryEntryAdditionalSubcategories
                .Where(a => a.DirectoryEntryId == directoryEntryId)
                .ToListAsync(ct);

            // Remove rows no longer wanted.
            foreach (var old in existing.Where(e => wanted.All(w => w.SubCategoryId != e.SubCategoryId)))
            {
                this.db.DirectoryEntryAdditionalSubcategories.Remove(old);
            }

            var now = DateTime.UtcNow;
            foreach (var w in wanted)
            {
                var row = existing.FirstOrDefault(e => e.SubCategoryId == w.SubCategoryId);
                if (row == null)
                {
                    row = new DirectoryEntryAdditionalSubcategory
                    {
                        DirectoryEntryId = directoryEntryId,
                        SubCategoryId = w.SubCategoryId,
                        CreateDate = now,
                    };
                    this.db.DirectoryEntryAdditionalSubcategories.Add(row);
                }
                else
                {
                    row.UpdateDate = now;
                }

                row.Name = Clean(w.Name);
                row.Link = Clean(w.Link);
                row.LinkA = Clean(w.LinkA);
                row.Link2 = Clean(w.Link2);
                row.Link2A = Clean(w.Link2A);
                row.Link3 = Clean(w.Link3);
                row.Link3A = Clean(w.Link3A);
                row.Description = Clean(w.Description);
            }

            await this.db.SaveChangesAsync(ct);
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
