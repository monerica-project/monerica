using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SponsoredListingInvoiceRepository : ISponsoredListingInvoiceRepository
    {
        private readonly IApplicationDbContext context;

        public SponsoredListingInvoiceRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<SponsoredListingInvoice?> GetByIdAsync(int sponsoredListingInvoiceId)
        {
            return await this.context.SponsoredListingInvoices.FindAsync(sponsoredListingInvoiceId);
        }

        public async Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId)
        {
            return await this.context.SponsoredListingInvoices
                                     .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
        }

        public async Task<SponsoredListingInvoice?> GetByReservationGuidAsync(Guid reservationGuid)
        {
            return await this.context.SponsoredListingInvoices
                                     .FirstOrDefaultAsync(x => x.ReservationGuid == reservationGuid);
        }

        public async Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync()
        {
            return await this.context.SponsoredListingInvoices.ToListAsync();
        }

        public async Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice)
        {
            await this.context.SponsoredListingInvoices.AddAsync(invoice);
            await this.context.SaveChangesAsync();

            return invoice;
        }

        public async Task<bool> UpdateAsync(SponsoredListingInvoice invoice)
        {
            try
            {
                this.context.SponsoredListingInvoices.Update(invoice);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageAsync(int page, int pageSize)
        {
            var totalItems = await this.context.SponsoredListingInvoices.CountAsync();
            var invoices = await this.context.SponsoredListingInvoices
                                             .OrderByDescending(i => i.CreateDate)
                                             .Skip((page - 1) * pageSize)
                                             .Take(pageSize)
                                             .ToListAsync();

            return (invoices, totalItems);
        }

        public async Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageByTypeAsync(
            int page,
            int pageSize,
            Enums.PaymentStatus paymentStatus)
        {
            var totalItems = await this.context
                                       .SponsoredListingInvoices
                                       .Where(x => x.PaymentStatus == paymentStatus).CountAsync();
            var invoices = await this.context.SponsoredListingInvoices
                                             .OrderByDescending(i => i.CreateDate)
                                             .Where(x => x.PaymentStatus == paymentStatus)
                                             .Skip((page - 1) * pageSize)
                                             .Take(pageSize)
                                             .ToListAsync();

            return (invoices, totalItems);
        }

        public async Task DeleteAsync(int sponsoredListingInvoiceId)
        {
            var invoice = await this.GetByIdAsync(sponsoredListingInvoiceId);
            if (invoice != null)
            {
                this.context.SponsoredListingInvoices.Remove(invoice);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<InvoiceTotalsResult> GetTotalsPaidAsync(DateTime startDate, DateTime endDate)
        {
            var invoices = await this.context.SponsoredListingInvoices
                                             .Where(i => i.CreateDate >= startDate &&
                                                         i.CreateDate <= endDate &&
                                                         i.PaymentStatus == Enums.PaymentStatus.Paid)
                                             .ToListAsync();

            if (invoices == null || invoices.Count == 0)
            {
                return new InvoiceTotalsResult();
            }

            var result = new InvoiceTotalsResult
            {
                PaidInCurrency = invoices.First().PaidInCurrency,
                Currency = invoices.First().Currency,
                StartDate = startDate,
                EndDate = endDate,
                TotalReceivedAmount = invoices.Sum(i => i.OutcomeAmount),
                TotalAmount = invoices.Sum(i => i.Amount)
            };

            return result;
        }

        public async Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId)
        {
            var result = await this.context.SponsoredListingInvoices
                                     .FirstOrDefaultAsync(x => x.ProcessorInvoiceId == processorInvoiceId);

            return result ??
                throw new InvalidOperationException(
                    $"No SponsoredListingInvoice found for the provided {nameof(processorInvoiceId)}.");
        }

        public DateTime? GetLastPaidInvoiceUpdateDate()
        {
            // Fetch the latest CreateDate and UpdateDate
            var latestCreateDate = this.context.SponsoredListingInvoices
                                   .Where(e => e != null && e.PaymentStatus == Enums.PaymentStatus.Paid)
                                   .Max(e => (DateTime?)e.CreateDate);

            var latestUpdateDate = this.context.SponsoredListingInvoices
                                   .Where(e => e != null && e.PaymentStatus == Enums.PaymentStatus.Paid)
                                   .Max(e => e.UpdateDate) ?? DateTime.MinValue;

            // Return the more recent of the two dates
            return (DateTime)(latestCreateDate > latestUpdateDate ? latestCreateDate : latestUpdateDate);
        }

        public async Task<(IEnumerable<SponsoredListingInvoice> Invoices, int TotalCount)>
            GetInvoicesForDirectoryEntryAsync(int directoryEntryId, int page, int pageSize)
        {
            var baseQuery =
                from inv in this.context.SponsoredListingInvoices.AsNoTracking()
                join sl in this.context.SponsoredListings
                        .AsNoTracking()
                        .IgnoreQueryFilters() // if a global filter is hiding rows
                    on inv.SponsoredListingId equals sl.SponsoredListingId
                    into gj
                from sl in gj.DefaultIfEmpty()
                where inv.DirectoryEntryId == directoryEntryId // still filter on invoices
                orderby inv.CreateDate descending
                select inv;

            var total = await baseQuery.CountAsync();

            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public Task<bool> HasAnyPaidInvoiceForDirectoryEntryAsync(
            int directoryEntryId,
            int excludeSponsoredListingInvoiceId,
            CancellationToken ct = default) =>
            this.context.SponsoredListingInvoices.AsNoTracking()
                .AnyAsync(
                    i =>
                    i.DirectoryEntryId == directoryEntryId &&
                    i.PaymentStatus == PaymentStatus.Paid &&
                    i.SponsoredListingInvoiceId != excludeSponsoredListingInvoiceId, ct);

        public async Task<List<AdvertiserWindowStat>> GetAdvertiserWindowStatsAsync(
            DateTime windowStartDate,
            DateTime windowEndDate,
            SponsorshipType? sponsorshipType = null,
            bool paidOnly = true)
        {
            // date-only, inclusive window
            var from = windowStartDate.Date;
            var to = windowEndDate.Date;

            var q = this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(inv => inv.CampaignEndDate.Date >= from
                           && inv.CampaignStartDate.Date <= to);

            if (paidOnly)
            {
                q = q.Where(inv => inv.PaymentStatus == PaymentStatus.Paid);
            }

            if (sponsorshipType.HasValue)
            {
                q = q.Where(inv => inv.SponsorshipType == sponsorshipType.Value);
            }

            // Pull just what we need
            var list = await q
                .Select(inv => new
                {
                    inv.DirectoryEntryId,
                    inv.Amount,
                    inv.PaidAmount,
                    inv.CampaignStartDate,
                    inv.CampaignEndDate
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var groups = list.GroupBy(i => i.DirectoryEntryId).ToList();

            // One shot lookup for names (avoid N+1)
            var dirEntryIds = groups.Select(g => g.Key).Distinct().ToList();
            var nameLookup = await this.context.DirectoryEntries.AsNoTracking()
                .Where(de => dirEntryIds.Contains(de.DirectoryEntryId))
                .Select(de => new { de.DirectoryEntryId, de.Name })
                .ToDictionaryAsync(x => x.DirectoryEntryId, x => x.Name)
                .ConfigureAwait(false);

            var results = new List<AdvertiserWindowStat>(groups.Count);

            foreach (var grp in groups)
            {
                decimal revenue = 0m;
                double activeDays = 0d;
                int count = 0;

                foreach (var inv in grp)
                {
                    var campStart = inv.CampaignStartDate.Date;
                    var campEnd = inv.CampaignEndDate.Date;
                    var overlapFrom = campStart > from ? campStart : from;
                    var overlapTo = campEnd < to ? campEnd : to;

                    var overlapDays = (overlapTo - overlapFrom).TotalDays + 1; // inclusive
                    if (overlapDays <= 0)
                    {
                        continue;
                    }

                    count++;

                    var totalCampDays = (campEnd - campStart).TotalDays + 1;
                    if (totalCampDays <= 0)
                    {
                        totalCampDays = 1;
                    }

                    // Use PaidAmount when available; fallback to Amount
                    var baseAmount = inv.PaidAmount > 0m ? inv.PaidAmount : inv.Amount;

                    var fraction = overlapDays / totalCampDays;
                    revenue += baseAmount * (decimal)fraction;
                    activeDays += overlapDays;
                }

                results.Add(new AdvertiserWindowStat
                {
                    DirectoryEntryId = grp.Key,
                    DirectoryEntryName = nameLookup.TryGetValue(grp.Key, out var n) ? n : $"(#{grp.Key})",
                    RevenueInWindow = Math.Round(revenue, 2),
                    InvoiceCount = count,
                    OverlapDays = activeDays
                });
            }

            return results;
        }

        public async IAsyncEnumerable<AccountantRow> StreamPaidForAccountantAsync(
            DateTime startUtc,
            DateTime endUtc,
            SponsorshipType? sponsorshipType,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var q = this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            // Filter by UpdateDate primarily, but include rows where UpdateDate wasn't set and CreateDate is in range.
            q = q.Where(i =>
                (i.UpdateDate >= startUtc && i.UpdateDate <= endUtc) ||
                (i.UpdateDate == DateTime.MinValue && i.CreateDate >= startUtc && i.CreateDate <= endUtc));

            var rows = q
                .Select(i => new
                {
                    i.PaidAmount,
                    i.PaidInCurrency,
                    i.UpdateDate,
                    i.CreateDate,
                    i.Amount
                })
                .AsAsyncEnumerable()
                .WithCancellation(ct);

            await foreach (var r in rows)
            {
                var paidDate = r.UpdateDate != DateTime.MinValue ? r.UpdateDate : r.CreateDate;

                var description = r.PaidInCurrency switch
                {
                    Currency.BTC => "Bitcoin",
                    Currency.XMR => "Monero",
                    _ => r.PaidInCurrency.ToString()
                };

                yield return new AccountantRow
                {
                    Quantity = r.PaidAmount,
                    Description = description,
                    PaidDateUtc = (DateTime)paidDate,
                    SalesPrice = r.Amount,
                    Cost = r.Amount
                };
            }
        }

        // in SponsoredListingInvoiceRepository
        public async Task<(IEnumerable<SponsoredListingInvoice> Invoices, int TotalCount)>
            GetInvoicesForDirectoryEntryInWindowAsync(
                int directoryEntryId,
                DateTime windowStartUtc,
                DateTime windowEndUtc,
                SponsorshipType? sponsorshipType,
                bool paidOnly,
                bool useCampaignOverlap,
                int page,
                int pageSize)
        {
            var q = this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(i => i.DirectoryEntryId == directoryEntryId);

            if (paidOnly)
            {
                q = q.Where(i => i.PaymentStatus == PaymentStatus.Paid);
            }

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            // match the breakdown: include invoices whose CAMPAIGN overlaps the window
            if (useCampaignOverlap)
            {
                q = q.Where(i => i.CampaignEndDate >= windowStartUtc &&
                                 i.CampaignStartDate <= windowEndUtc);
            }
            else
            {
                q = q.Where(i => i.CreateDate >= windowStartUtc &&
                                 i.CreateDate <= windowEndUtc);
            }

            var total = await q.CountAsync();

            var items = await q
                .OrderByDescending(i => i.CreateDate) // keep your current sort
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<List<AdvertiserWindowSum>> GetAdvertiserInvoiceWindowSumsAsync(
            DateTime windowStartUtc,
            DateTime windowEndOpenUtc,
            SponsorshipType? sponsorshipType = null)
        {
            // Half-open [start, end)
            var q = this.context.SponsoredListingInvoices.AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid)
                .Where(i => i.CreateDate >= windowStartUtc && i.CreateDate < windowEndOpenUtc);

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            // Pull only what we need to memory (campaign-day math is simpler & safe here)
            var raw = await q
                .Select(i => new
                {
                    i.DirectoryEntryId,
                    i.Amount, // USD (your requirement)
                    i.CampaignStartDate,
                    i.CampaignEndDate
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var groups = raw.GroupBy(x => x.DirectoryEntryId);

            // One-shot name lookup to avoid N+1
            var ids = groups.Select(g => g.Key).Distinct().ToList();
            var names = await this.context.DirectoryEntries.AsNoTracking()
                .Where(de => ids.Contains(de.DirectoryEntryId))
                .Select(de => new { de.DirectoryEntryId, de.Name })
                .ToDictionaryAsync(x => x.DirectoryEntryId, x => x.Name)
                .ConfigureAwait(false);

            var results = new List<AdvertiserWindowSum>(ids.Count);

            foreach (var g in groups)
            {
                decimal revenue = 0m;
                int invoices = 0;
                long daysPurchased = 0;

                foreach (var i in g)
                {
                    // inclusive day count; guard against inverted dates
                    var days = (i.CampaignEndDate.Date - i.CampaignStartDate.Date).Days + 1;
                    if (days <= 0)
                    {
                        days = 1;
                    }

                    revenue += i.Amount;
                    invoices++;
                    daysPurchased += days;
                }

                if (daysPurchased <= 0)
                {
                    daysPurchased = 1;
                }

                results.Add(new AdvertiserWindowSum
                {
                    DirectoryEntryId = g.Key,
                    DirectoryEntryName = names.TryGetValue(g.Key, out var n) ? n : $"(#{g.Key})",
                    Revenue = Math.Round(revenue, 2),
                    Count = invoices,
                    DaysPurchased = (int)daysPurchased
                });
            }

            return results;
        }
    }
}