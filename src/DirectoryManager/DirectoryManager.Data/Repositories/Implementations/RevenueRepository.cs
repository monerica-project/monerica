using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class RevenueRepository : IRevenueRepository
    {
        private readonly IApplicationDbContext context;

        public RevenueRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<RevenueItem>> GetOneTimeRevenueAsync(CancellationToken ct = default)
        {
            var items = new List<RevenueItem>();

            var video = await this.context.VideoSponsorOrders.AsNoTracking()
                .Where(o => (o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.OverPayment)
                            && o.Currency == Currency.USD)
                .Select(o => new { Paid = o.PaidUtc ?? o.CreateDate, o.Amount })
                .ToListAsync(ct);
            items.AddRange(video.Select(o => new RevenueItem
            {
                Source = RevenueSource.VideoSponsorships,
                PaidUtc = o.Paid,
                AmountUsd = o.Amount,
            }));

            var affiliate = await this.context.AffiliateCommissionsEarned.AsNoTracking()
                .Select(a => new { a.CommissionDate, a.UsdValue })
                .ToListAsync(ct);
            items.AddRange(affiliate.Select(a => new RevenueItem
            {
                Source = RevenueSource.AffiliateCommissions,
                PaidUtc = a.CommissionDate,
                AmountUsd = a.UsdValue,
            }));

            var submissions = await this.context.Submissions.AsNoTracking()
                .Where(s => s.PaidUtc != null)
                .Select(s => new { Paid = s.PaidUtc!.Value, s.PaidUsd })
                .ToListAsync(ct);
            items.AddRange(submissions.Select(s => Donation(RevenueSource.SubmissionReviews, s.Paid, s.PaidUsd)));

            var verifications = await this.context.VerificationRequests.AsNoTracking()
                .Where(v => v.PaidUtc != null)
                .Select(v => new { Paid = v.PaidUtc!.Value, v.PaidUsd })
                .ToListAsync(ct);
            items.AddRange(verifications.Select(v => Donation(RevenueSource.VerificationReviews, v.Paid, v.PaidUsd)));

            var problems = await this.context.ProblemReports.AsNoTracking()
                .Where(p => p.PaidUtc != null)
                .Select(p => new { Paid = p.PaidUtc!.Value, p.PaidUsd })
                .ToListAsync(ct);
            items.AddRange(problems.Select(p => Donation(RevenueSource.ProblemReportReviews, p.Paid, p.PaidUsd)));

            return items;
        }

        private static RevenueItem Donation(RevenueSource source, DateTime paidUtc, decimal? usd) => new ()
        {
            Source = source,
            PaidUtc = paidUtc,
            AmountUsd = usd ?? 0m,
            MissingUsd = usd is null,
        };
    }
}
