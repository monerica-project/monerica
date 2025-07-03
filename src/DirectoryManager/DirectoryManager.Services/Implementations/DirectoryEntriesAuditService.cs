using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Interfaces;

namespace DirectoryManager.Services.Implementations
{
    public class DirectoryEntriesAuditService : IDirectoryEntriesAuditService
    {
        private readonly IDirectoryEntriesAuditRepository repo;

        public DirectoryEntriesAuditService(IDirectoryEntriesAuditRepository repo)
        {
            this.repo = repo;
        }

        public async Task<List<MonthlyAuditReport>> GetMonthlyReportAsync()
        {
            var audits = await this.repo.GetAllAsync();

            // 1) First audit per entry => addition
            var firstCreates = audits
                .GroupBy(a => a.DirectoryEntryId)
                .Select(g => g.OrderBy(a => a.CreateDate).First())
                .ToList();

            // 2) Any audit marking “Removed” => removal
            var removalRows = audits
                .Where(a => a.DirectoryStatus == Data.Enums.DirectoryStatus.Removed && a.UpdateDate.HasValue)
                .ToList();

            // 3) Aggregate counts by Year/Month
            var addsByMonth = firstCreates
                .GroupBy(a => new { a.CreateDate.Year, a.CreateDate.Month })
                .ToDictionary(
                    g => (g.Key.Year, g.Key.Month),
                    g => g.Count());

            var remsByMonth = removalRows
                .GroupBy(a => new { Year = a.UpdateDate.Value.Year, Month = a.UpdateDate.Value.Month })
                .ToDictionary(
                    g => (g.Key.Year, g.Key.Month),
                    g => g.Select(x => x.DirectoryEntryId).Distinct().Count());

            // 4) build a sorted list of all months present
            var allMonths = addsByMonth.Keys
                .Union(remsByMonth.Keys)
                .Select(k => new DateTime(k.Year, k.Month, 1))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // 5) compute running totals
            var report = new List<MonthlyAuditReport>();
            int cumAdds = 0, cumRems = 0;

            foreach (var dt in allMonths)
            {
                var key = (dt.Year, dt.Month);
                int adds = addsByMonth.TryGetValue(key, out var a) ? a : 0;
                int rems = remsByMonth.TryGetValue(key, out var r) ? r : 0;

                cumAdds += adds;
                cumRems += rems;

                report.Add(new MonthlyAuditReport
                {
                    Year = dt.Year,
                    Month = dt.Month,
                    Additions = adds,
                    Removals = rems,
                    ActiveCount = Math.Max(0, cumAdds - cumRems)
                });
            }

            return report;
        }
    }
}