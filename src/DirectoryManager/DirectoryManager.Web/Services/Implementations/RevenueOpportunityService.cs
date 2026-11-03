using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Revenue;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Builds the revenue-opportunity reports: abandoned checkouts, sponsors to renew or win back,
    /// and popular listings that have never sponsored. Test checkouts are always left out.
    /// </summary>
    public class RevenueOpportunityService
    {
        public const int AbandonedDefaultDays = 60;
        public const int ExpiringWithinDays = 30;
        public const int LapsedWithinDays = 180;
        public const int ProspectTake = 100;

        private const string SummaryCacheKey = "revenue-opportunity-summary";

        private readonly IRevenueOpportunityRepository repo;
        private readonly IDirectoryEntryRepository entryRepo;
        private readonly ICategoryRepository categoryRepo;
        private readonly ISubcategoryRepository subcategoryRepo;
        private readonly ISponsoredListingOpeningNotificationRepository waitlistRepo;
        private readonly ISponsorInterestLogRepository interestRepo;
        private readonly IMemoryCache cache;
        private readonly Dictionary<(SponsorshipType, int?), int> waitlistCounts = new ();

        private Dictionary<int, string>? catNames;
        private Dictionary<int, string>? subLabels;

        public RevenueOpportunityService(
            IRevenueOpportunityRepository repo,
            IDirectoryEntryRepository entryRepo,
            ICategoryRepository categoryRepo,
            ISubcategoryRepository subcategoryRepo,
            ISponsoredListingOpeningNotificationRepository waitlistRepo,
            ISponsorInterestLogRepository interestRepo,
            IMemoryCache cache)
        {
            this.repo = repo;
            this.entryRepo = entryRepo;
            this.categoryRepo = categoryRepo;
            this.subcategoryRepo = subcategoryRepo;
            this.waitlistRepo = waitlistRepo;
            this.interestRepo = interestRepo;
            this.cache = cache;
        }

        public static void ClearSummaryCache(IMemoryCache cache) => cache.Remove(SummaryCacheKey);

        internal static bool IsPaid(PaymentStatus s) => s is PaymentStatus.Paid or PaymentStatus.OverPayment;

        /// <summary>Statuses that mean "started paying, never finished".</summary>
        internal static bool IsUnfinished(PaymentStatus s) =>
            s is not (PaymentStatus.Paid or PaymentStatus.OverPayment or PaymentStatus.Refunded or PaymentStatus.Test);

        internal static int? ScopeId(SponsorshipType type, int? categoryId, int? subCategoryId) => type switch
        {
            SponsorshipType.CategorySponsor => categoryId,
            SponsorshipType.SubcategorySponsor => subCategoryId,
            _ => null,
        };

        public async Task<RevenueSummary> GetSummaryAsync()
        {
            if (this.cache.TryGetValue(SummaryCacheKey, out RevenueSummary? cached) && cached != null)
            {
                return cached;
            }

            var now = DateTime.UtcNow;
            var interest = await this.interestRepo.GetInRangeAsync(now.AddDays(-7), now.AddMinutes(1));
            var (expiring, lapsed) = await this.GetRenewalsAsync(withWaitlist: false);
            var summary = new RevenueSummary
            {
                AbandonedLast30Days = (await this.GetAbandonedAsync(30, withWaitlist: false)).Count,
                ExpiringIn30Days = expiring.Count,
                Lapsed = lapsed.Count,
                Prospects = (await this.GetProspectsAsync()).Count(p => p.Views30 > 0),
                InterestLast7Days = interest.Select(i => i.DirectoryEntryId).Distinct().Count(),
            };
            this.cache.Set(SummaryCacheKey, summary, TimeSpan.FromMinutes(10));
            return summary;
        }

        public async Task<List<AbandonedCheckoutRow>> GetAbandonedAsync(int days, bool withWaitlist = true)
        {
            var invoices = await this.repo.GetInvoicesSinceAsync(DateTime.UtcNow.AddDays(-days));
            var paid = invoices.Where(i => IsPaid(i.PaymentStatus)).ToList();
            var rows = new List<AbandonedCheckoutRow>();

            foreach (var g in invoices.Where(i => IsUnfinished(i.PaymentStatus))
                         .GroupBy(i => (i.DirectoryEntryId, i.SponsorshipType, Scope: ScopeId(i.SponsorshipType, i.CategoryId, i.SubCategoryId))))
            {
                var first = g.Min(i => i.CreateDate);

                // Bought it after all (maybe on a second try): not abandoned.
                if (paid.Any(p => p.DirectoryEntryId == g.Key.DirectoryEntryId
                                  && p.SponsorshipType == g.Key.SponsorshipType
                                  && ScopeId(p.SponsorshipType, p.CategoryId, p.SubCategoryId) == g.Key.Scope
                                  && p.CreateDate >= first))
                {
                    continue;
                }

                var latest = g.OrderByDescending(i => i.CreateDate).First();
                rows.Add(new AbandonedCheckoutRow
                {
                    LatestInvoiceId = latest.SponsoredListingInvoiceId,
                    DirectoryEntryId = g.Key.DirectoryEntryId,
                    Name = latest.DirectoryEntry?.Name ?? "#" + g.Key.DirectoryEntryId,
                    ProfilePath = latest.DirectoryEntry != null ? "/site/" + latest.DirectoryEntry.DirectoryEntryKey : null,
                    Placement = await this.PlacementAsync(g.Key.SponsorshipType, g.Key.Scope),
                    Email = g.Select(i => i.Email).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                    Amount = latest.Amount,
                    Currency = latest.Currency.ToString(),
                    Attempts = g.Count(),
                    LastAttempt = latest.CreateDate,
                    Status = latest.PaymentStatus.ToString(),
                    WaitlistCount = withWaitlist ? await this.WaitlistAsync(g.Key.SponsorshipType, g.Key.Scope) : 0,
                });
            }

            return rows.OrderByDescending(r => r.LastAttempt).ToList();
        }

        public async Task<(List<RenewalRow> Expiring, List<RenewalRow> Lapsed)> GetRenewalsAsync(bool withWaitlist = true)
        {
            var now = DateTime.UtcNow;
            var all = await this.repo.GetSponsorshipsEndingSinceAsync(now.AddDays(-LapsedWithinDays));
            var activeEntries = all.Where(s => s.CampaignStartDate <= now && s.CampaignEndDate > now)
                .Select(s => s.DirectoryEntryId).ToHashSet();

            var expiring = new List<RenewalRow>();
            var lapsed = new List<RenewalRow>();

            foreach (var g in all.GroupBy(s => (s.DirectoryEntryId, s.SponsorshipType, Scope: ScopeId(s.SponsorshipType, s.CategoryId, s.SubCategoryId))))
            {
                var end = g.Max(s => s.CampaignEndDate);
                bool isExpiring = end > now && end <= now.AddDays(ExpiringWithinDays);
                bool isLapsed = end <= now && !activeEntries.Contains(g.Key.DirectoryEntryId);
                if (!isExpiring && !isLapsed)
                {
                    continue;
                }

                var entry = g.Select(s => s.DirectoryEntry).FirstOrDefault(e => e != null);
                var invoices = g.Select(s => s.SponsoredListingInvoice).Where(i => i != null).Select(i => i!).ToList();
                var row = new RenewalRow
                {
                    DirectoryEntryId = g.Key.DirectoryEntryId,
                    Name = entry?.Name ?? "#" + g.Key.DirectoryEntryId,
                    ProfilePath = entry != null ? "/site/" + entry.DirectoryEntryKey : null,
                    Placement = await this.PlacementAsync(g.Key.SponsorshipType, g.Key.Scope),
                    EndsUtc = end,
                    DaysLeft = (int)Math.Ceiling((end - now).TotalDays),
                    TimesBought = g.Count(),
                    TotalPaidUsd = invoices.Where(i => i.Currency == Currency.USD).Sum(i => i.Amount),
                    Email = invoices.OrderByDescending(i => i.CreateDate).Select(i => i.Email).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                    WaitlistCount = withWaitlist ? await this.WaitlistAsync(g.Key.SponsorshipType, g.Key.Scope) : 0,
                    HasOtherActiveSponsorship = isExpiring && all.Any(s => s.DirectoryEntryId == g.Key.DirectoryEntryId
                        && s.CampaignStartDate <= now && s.CampaignEndDate > now
                        && (s.SponsorshipType != g.Key.SponsorshipType || ScopeId(s.SponsorshipType, s.CategoryId, s.SubCategoryId) != g.Key.Scope)),
                };
                (isExpiring ? expiring : lapsed).Add(row);
            }

            return (expiring.OrderBy(r => r.EndsUtc).ToList(), lapsed.OrderByDescending(r => r.EndsUtc).ToList());
        }

        public async Task<List<ProspectRow>> GetProspectsAsync()
        {
            var now = DateTime.UtcNow;
            var everPaid = await this.repo.GetEverPaidEntryIdsAsync();
            var views = await this.repo.GetListingPageViewsAsync();
            var looks = (await this.interestRepo.GetInRangeAsync(now.AddDays(-90), now.AddMinutes(1)))
                .GroupBy(l => l.DirectoryEntryId).ToDictionary(g => g.Key, g => g.Count());
            var minListed = now.AddDays(-DirectoryManager.Common.Constants.IntegerConstants.MinimumDaysListedBeforeSponsoring);
            await this.EnsureNamesAsync();

            return (await this.entryRepo.GetActiveEntriesByStatusAsync(DirectoryStatus.Verified))
                .Where(e => !everPaid.Contains(e.DirectoryEntryId) && e.CreateDate <= minListed)
                .Select(e =>
                {
                    views.TryGetValue(e.DirectoryEntryKey, out var v);
                    return new ProspectRow
                    {
                        DirectoryEntryId = e.DirectoryEntryId,
                        Name = e.Name,
                        ProfilePath = "/site/" + e.DirectoryEntryKey,
                        Category = this.subLabels!.TryGetValue(e.SubCategoryId, out var sl) ? sl : string.Empty,
                        Views30 = v?.Views30 ?? 0,
                        Views90 = v?.Views90 ?? 0,
                        InterestLooks = looks.TryGetValue(e.DirectoryEntryId, out var n) ? n : 0,
                        Email = e.Email,
                    };
                })
                .OrderByDescending(p => p.Views30)
                .ThenByDescending(p => p.Views90)
                .ThenByDescending(p => p.InterestLooks)
                .Take(ProspectTake)
                .ToList();
        }

        public async Task<DateTime?> GetViewsUpdatedUtcAsync()
        {
            var views = await this.repo.GetListingPageViewsAsync();
            return views.Count == 0 ? null : views.Values.Max(v => v.UpdatedUtc);
        }

        private async Task EnsureNamesAsync()
        {
            if (this.catNames != null)
            {
                return;
            }

            this.catNames = (await this.categoryRepo.GetAllAsync()).ToDictionary(c => c.CategoryId, c => c.Name);
            var cats = this.catNames;
            this.subLabels = (await this.subcategoryRepo.GetAllAsync()).ToDictionary(
                s => s.SubCategoryId,
                s => $"{(cats.TryGetValue(s.CategoryId, out var cn) ? cn : "#" + s.CategoryId)} › {s.Name}");
        }

        private async Task<string> PlacementAsync(SponsorshipType type, int? scope)
        {
            await this.EnsureNamesAsync();
            return type switch
            {
                SponsorshipType.MainSponsor => "Main sponsor",
                SponsorshipType.CategorySponsor => "Category: " + (scope is int c && this.catNames!.TryGetValue(c, out var n) ? n : "?"),
                SponsorshipType.SubcategorySponsor => "Subcategory: " + (scope is int s && this.subLabels!.TryGetValue(s, out var sn) ? sn : "?"),
                _ => type.ToString(),
            };
        }

        private async Task<int> WaitlistAsync(SponsorshipType type, int? scope)
        {
            if (!this.waitlistCounts.TryGetValue((type, scope), out var n))
            {
                n = await this.waitlistRepo.GetWaitlistCountAsync(type, type == SponsorshipType.MainSponsor ? null : scope);
                this.waitlistCounts[(type, scope)] = n;
            }

            return n;
        }
    }
}
