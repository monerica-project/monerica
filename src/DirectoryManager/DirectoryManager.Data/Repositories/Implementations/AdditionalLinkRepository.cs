using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public sealed class AdditionalLinkRepository : IAdditionalLinkRepository
    {
        private const int MaxLinks = 3;

        private readonly IApplicationDbContext context;

        public AdditionalLinkRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task<IReadOnlyList<AdditionalLink>> GetByDirectoryEntryIdAsync(
            int directoryEntryId,
            CancellationToken ct)
        {
            return await this.context.AdditionalLinks
                .AsNoTracking()
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AdditionalLink>> UpsertForDirectoryEntryAsync(
            int directoryEntryId,
            IEnumerable<string?> links,
            CancellationToken ct)
        {
            var normalized = (links ?? Array.Empty<string?>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxLinks)
                .ToList();

            var existing = await this.context.AdditionalLinks
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);

            if (normalized.Count == 0)
            {
                if (existing.Count > 0)
                {
                    this.context.AdditionalLinks.RemoveRange(existing);
                    await this.context.SaveChangesAsync(ct);
                }

                return Array.Empty<AdditionalLink>();
            }

            for (int i = 0; i < normalized.Count; i++)
            {
                var sortOrder = i + 1;
                var linkValue = normalized[i];

                var row = existing.FirstOrDefault(x => x.SortOrder == sortOrder);

                if (row is null)
                {
                    this.context.AdditionalLinks.Add(new AdditionalLink
                    {
                        DirectoryEntryId = directoryEntryId,
                        SortOrder = sortOrder,
                        Link = linkValue
                    });
                }
                else
                {
                    row.Link = linkValue;
                    this.context.AdditionalLinks.Update(row);
                }
            }

            var toRemove = existing.Where(x => x.SortOrder > normalized.Count).ToList();
            if (toRemove.Count > 0)
            {
                this.context.AdditionalLinks.RemoveRange(toRemove);
            }

            await this.context.SaveChangesAsync(ct);

            return await this.GetByDirectoryEntryIdAsync(directoryEntryId, ct);
        }

        public async Task DeleteForDirectoryEntryAsync(int directoryEntryId, CancellationToken ct)
        {
            var rows = await this.context.AdditionalLinks
                .Where(x => x.DirectoryEntryId == directoryEntryId)
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                return;
            }

            this.context.AdditionalLinks.RemoveRange(rows);
            await this.context.SaveChangesAsync(ct);
        }
    }
}
