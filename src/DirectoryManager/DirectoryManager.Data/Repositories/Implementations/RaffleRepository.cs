using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class RaffleRepository : IRaffleRepository
    {
        private readonly IApplicationDbContext context;

        public RaffleRepository(IApplicationDbContext context) => this.context = context;

        private DbSet<Raffle> Set => this.context.Raffles;

        private DbSet<DirectoryEntryReviewRaffleEntry> Entries =>
            this.context.DirectoryEntryReviewRaffleEntries;

        public async Task<Raffle?> GetByIdAsync(int id, CancellationToken ct = default) =>
            await this.Set.FindAsync(new object[] { id }, ct);

        public async Task<Raffle?> GetActiveAsync(DateTime utcNow, CancellationToken ct = default)
        {
            return await this.Set.AsNoTracking()
                .Where(r => r.IsEnabled
                         && r.StartDate <= utcNow
                         && r.EndDate >= utcNow)
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RaffleId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<Raffle>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            return await this.Set.AsNoTracking()
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RaffleId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public Task<int> CountAsync(CancellationToken ct = default) =>
            this.Set.CountAsync(ct);

        public async Task<List<RaffleSummaryDto>> ListWithCountsAsync(
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 50;
            }

            // Page the raffles first.
            var raffles = await this.Set.AsNoTracking()
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RaffleId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RaffleSummaryDto
                {
                    RaffleId = r.RaffleId,
                    Name = r.Name,
                    StartDate = r.StartDate,
                    EndDate = r.EndDate,
                    IsEnabled = r.IsEnabled
                })
                .ToListAsync(ct);

            if (raffles.Count == 0)
            {
                return raffles;
            }

            var ids = raffles.Select(r => r.RaffleId).ToList();

            // Aggregated counts in a single round-trip.
            var grouped = await this.Entries.AsNoTracking()
                .Where(e => e.RaffleId.HasValue && ids.Contains(e.RaffleId.Value))
                .GroupBy(e => new { RaffleId = e.RaffleId!.Value, e.Status })
                .Select(g => new
                {
                    g.Key.RaffleId,
                    g.Key.Status,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            var byRaffle = grouped
                .GroupBy(x => x.RaffleId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var r in raffles)
            {
                if (!byRaffle.TryGetValue(r.RaffleId, out var rows))
                {
                    continue;
                }

                foreach (var row in rows)
                {
                    switch (row.Status)
                    {
                        case RaffleEntryStatus.Pending:
                            r.PendingCount = row.Count;
                            break;
                        case RaffleEntryStatus.Eligible:
                            r.EligibleCount = row.Count;
                            break;
                        case RaffleEntryStatus.Paid:
                            r.PaidCount = row.Count;
                            break;
                        case RaffleEntryStatus.Disqualified:
                            r.DisqualifiedCount = row.Count;
                            break;
                        case RaffleEntryStatus.Ended:
                            r.EndedCount = row.Count;
                            break;
                    }

                    r.TotalEntries += row.Count;
                }
            }

            return raffles;
        }

        public async Task AddAsync(Raffle entity, CancellationToken ct = default)
        {
            entity.CreateDate = DateTime.UtcNow;
            entity.UpdateDate = null;
            this.Set.Add(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(Raffle entity, CancellationToken ct = default)
        {
            entity.UpdateDate = DateTime.UtcNow;
            this.Set.Update(entity);
            await this.context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await this.Set.FindAsync(new object[] { id }, ct);
            if (existing is null)
            {
                return;
            }

            this.Set.Remove(existing);
            await this.context.SaveChangesAsync(ct);
        }
    }
}
