using System.Runtime.CompilerServices;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

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
            return await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.SponsoredListingInvoiceId == sponsoredListingInvoiceId);
        }

        public async Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId)
        {
            return await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);
        }

        public async Task<SponsoredListingInvoice?> GetByReservationGuidAsync(Guid reservationGuid)
        {
            return await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ReservationGuid == reservationGuid);
        }

        public async Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync()
        {
            return await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .OrderByDescending(i => i.CreateDate)
                .ToListAsync();
        }

        public async Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice)
        {
            // Insert and persist
            await this.context.SponsoredListingInvoices.AddAsync(invoice);
            await this.context.SaveChangesAsync();

            // Re-read the row with includes and AsNoTracking so the caller gets a detached snapshot
            var snapshot = await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.SponsoredListingInvoiceId == invoice.SponsoredListingInvoiceId);

            // Fallback to the inserted instance if the re-read ever fails
            return snapshot ?? invoice;
        }

        public async Task<bool> UpdateAsync(SponsoredListingInvoice invoice)
        {
            try
            {
                // Load the tracked instance (EF will return the already-tracked one if present)
                var existing = await this.context.SponsoredListingInvoices
                    .FirstOrDefaultAsync(i => i.SponsoredListingInvoiceId == invoice.SponsoredListingInvoiceId);

                if (existing == null)
                {
                    return false; // nothing to update
                }

                // Copy only the mutable fields
                CopyMutableFields(existing, invoice);

                // Optional: keep an audit timestamp if your model has UpdateDate
                existing.UpdateDate = DateTime.UtcNow;

                await this.context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageAsync(int page, int pageSize)
        {
            var baseQuery = WithIncludes(this.context.SponsoredListingInvoices).AsNoTracking();

            var totalItems = await baseQuery.CountAsync();

            var invoices = await baseQuery
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
            var baseQuery = WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .Where(x => x.PaymentStatus == paymentStatus);

            var totalItems = await baseQuery.CountAsync();

            var invoices = await baseQuery
                .OrderByDescending(i => i.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (invoices, totalItems);
        }

        public async Task DeleteAsync(int sponsoredListingInvoiceId)
        {
            var invoice = await this.context.SponsoredListingInvoices.FindAsync(sponsoredListingInvoiceId);
            if (invoice != null)
            {
                this.context.SponsoredListingInvoices.Remove(invoice);
                await this.context.SaveChangesAsync();
            }
        }

        public async Task<InvoiceTotalsResult> GetTotalsPaidAsync(DateTime startDate, DateTime endDate)
        {
            var invoices = await this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(i => i.CreateDate >= startDate &&
                            i.CreateDate <= endDate &&
                            i.PaymentStatus == Enums.PaymentStatus.Paid)
                .ToListAsync();

            if (invoices.Count == 0)
            {
                return new InvoiceTotalsResult();
            }

            return new InvoiceTotalsResult
            {
                PaidInCurrency = invoices.First().PaidInCurrency,
                Currency = invoices.First().Currency,
                StartDate = startDate,
                EndDate = endDate,
                TotalReceivedAmount = invoices.Sum(i => i.OutcomeAmount),
                TotalAmount = invoices.Sum(i => i.Amount)
            };
        }

        public async Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId)
        {
            var result = await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProcessorInvoiceId == processorInvoiceId);

            return result ??
                throw new InvalidOperationException(
                    $"No SponsoredListingInvoice found for the provided {nameof(processorInvoiceId)}.");
        }

        public DateTime? GetLastPaidInvoiceUpdateDate()
        {
            var latestCreateDate = this.context.SponsoredListingInvoices
                                   .Where(e => e.PaymentStatus == Enums.PaymentStatus.Paid)
                                   .Max(e => (DateTime?)e.CreateDate);

            var latestUpdateDate = this.context.SponsoredListingInvoices
                                   .Where(e => e.PaymentStatus == Enums.PaymentStatus.Paid)
                                   .Max(e => e.UpdateDate) ?? DateTime.MinValue;

            return (DateTime)(latestCreateDate > latestUpdateDate ? latestCreateDate : latestUpdateDate);
        }

        public async Task<(IEnumerable<SponsoredListingInvoice> Invoices, int TotalCount)>
            GetInvoicesForDirectoryEntryAsync(int directoryEntryId, int page, int pageSize)
        {
            var baseQuery = WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .Where(inv => inv.DirectoryEntryId == directoryEntryId)
                .OrderByDescending(inv => inv.CreateDate);

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
            // (kept your existing logic – no need to include DirectoryEntry here,
            // because names are looked up separately)
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

                    var overlapDays = (overlapTo - overlapFrom).TotalDays; // inclusive
                    if (overlapDays <= 0)
                    {
                        continue;
                    }

                    count++;

                    var totalCampDays = (campEnd - campStart).TotalDays;
                    if (totalCampDays <= 0)
                    {
                        totalCampDays = 1;
                    }

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
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var q = this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid);

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

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
            var q = WithIncludes(this.context.SponsoredListingInvoices)
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
                .OrderByDescending(i => i.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<SponsoredListingInvoice?> GetByProcessorInvoiceIdAsync(string value)
        {
            return await WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProcessorInvoiceId == value);
        }

        public async Task<List<AdvertiserWindowSum>> GetAdvertiserInvoiceWindowSumsAsync(
            DateTime windowStartUtc,
            DateTime windowEndOpenUtc,
            SponsorshipType? sponsorshipType = null)
        {
            // This method returns sums; names are resolved via a one-shot lookup below.
            var q = this.context.SponsoredListingInvoices.AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid)
                .Where(i => i.CreateDate >= windowStartUtc && i.CreateDate < windowEndOpenUtc);

            if (sponsorshipType.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == sponsorshipType.Value);
            }

            var raw = await q
                .Select(i => new
                {
                    i.DirectoryEntryId,
                    i.Amount,
                    i.CampaignStartDate,
                    i.CampaignEndDate
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var groups = raw.GroupBy(x => x.DirectoryEntryId);

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
                    var days = (i.CampaignEndDate.Date - i.CampaignStartDate.Date).Days;
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

        public async Task<List<SponsoredListingInvoice>> GetPaidInvoicesAsync(
            DateTime fromUtc,
            DateTime toUtc,
            SponsorshipType? type = null,
            int? subCategoryId = null,
            int? categoryId = null,
            CancellationToken ct = default)
        {
            // Find any PAID invoice whose campaign window overlaps [fromUtc, toUtc)
            var q = this.context.SponsoredListingInvoices.AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid &&
                            i.CampaignEndDate >= fromUtc &&
                            i.CampaignStartDate < toUtc);

            if (type.HasValue)
            {
                q = q.Where(i => i.SponsorshipType == type.Value);
            }

            if (subCategoryId.HasValue)
            {
                q = q.Where(i => i.SubCategoryId == subCategoryId.Value);
            }

            if (categoryId.HasValue)
            {
                q = q.Where(i => i.CategoryId == categoryId.Value);
            }

            // Project minimal fields needed for churn logic
            return await q
                .Select(i => new SponsoredListingInvoice
                {
                    DirectoryEntryId = i.DirectoryEntryId,
                    SponsorshipType = i.SponsorshipType,
                    SubCategoryId = i.SubCategoryId,
                    CategoryId = i.CategoryId,
                    CampaignStartDate = i.CampaignStartDate,
                    CampaignEndDate = i.CampaignEndDate
                })
                .ToListAsync(ct);
        }

        public async Task<List<RecentPaidPurchaseDto>> GetRecentPaidActivePurchasesAsync(int take)
        {
            take = Math.Max(1, take);

            // Base PAID invoices (ONLY for Admitted/Verified listings)
            var baseQ = WithIncludes(this.context.SponsoredListingInvoices)
                .AsNoTracking()
                .Where(i => i.PaymentStatus == PaymentStatus.Paid)
                .Where(i =>
                    i.DirectoryEntry != null &&
                    (i.DirectoryEntry.DirectoryStatus == DirectoryStatus.Admitted ||
                     i.DirectoryEntry.DirectoryStatus == DirectoryStatus.Verified));

            // 1) For each (DirectoryEntryId, SponsorshipType), find the MAX CreateDate
            var maxCreatePerGroup = baseQ
                .GroupBy(i => new { i.DirectoryEntryId, i.SponsorshipType })
                .Select(g => new
                {
                    g.Key.DirectoryEntryId,
                    g.Key.SponsorshipType,
                    MaxCreateDate = g.Max(x => x.CreateDate)
                });

            // 2) Join back to get rows that match the latest CreateDate for that listing+type
            var latestByCreateDate =
                from i in baseQ
                join m in maxCreatePerGroup
                    on new { i.DirectoryEntryId, i.SponsorshipType, i.CreateDate }
                    equals new { m.DirectoryEntryId, m.SponsorshipType, CreateDate = m.MaxCreateDate }
                select i;

            // 3) Tie-break: if multiple invoices share the same CreateDate, take the max InvoiceId
            var maxIdPerGroupAtMaxCreate = latestByCreateDate
                .GroupBy(i => new { i.DirectoryEntryId, i.SponsorshipType })
                .Select(g => new
                {
                    g.Key.DirectoryEntryId,
                    g.Key.SponsorshipType,
                    MaxInvoiceId = g.Max(x => x.SponsoredListingInvoiceId)
                });

            var picked =
                from i in latestByCreateDate
                join pickMax in maxIdPerGroupAtMaxCreate
                    on new { i.DirectoryEntryId, i.SponsorshipType, i.SponsoredListingInvoiceId }
                    equals new { pickMax.DirectoryEntryId, pickMax.SponsorshipType, SponsoredListingInvoiceId = pickMax.MaxInvoiceId }
                orderby i.CreateDate descending, i.SponsoredListingInvoiceId descending
                select new
                {
                    PaidDateUtc = i.CreateDate, // "bought date"
                    i.SponsorshipType,

                    AmountUsd = i.Amount,

                    i.PaidInCurrency,
                    i.PaidAmount,
                    i.OutcomeAmount,

                    i.CampaignStartDate,
                    i.CampaignEndDate,

                    i.DirectoryEntryId,
                    ListingName = i.DirectoryEntry != null ? (i.DirectoryEntry.Name ?? "") : "",
                    ListingUrl = i.DirectoryEntry != null ? (i.DirectoryEntry.Link ?? "") : ""
                };

            var rows = await picked
                .Take(take)
                .ToListAsync()
                .ConfigureAwait(false);

            // Map to DTO (do day math in memory to avoid provider quirks)
            return rows.Select(x =>
            {
                // ✅ inclusive days (+1)
                var days = (x.CampaignEndDate.Date - x.CampaignStartDate.Date).Days;
                if (days <= 0) days = 1;

                var paidAmount = x.PaidAmount > 0m ? x.PaidAmount : x.OutcomeAmount;

                return new RecentPaidPurchaseDto
                {
                    PaidDateUtc = x.PaidDateUtc,
                    SponsorshipType = x.SponsorshipType,

                    AmountUsd = x.AmountUsd,
                    Days = days,
                    PricePerDayUsd = Math.Round(x.AmountUsd / days, 2),

                    PaidCurrency = x.PaidInCurrency,
                    PaidAmount = paidAmount,

                    ExpiresUtc = x.CampaignEndDate,

                    DirectoryEntryId = x.DirectoryEntryId,
                    ListingName = x.ListingName,
                    ListingUrl = x.ListingUrl
                };
            }).ToList();
        }

        // Centralize eager-loading for all places that need DirectoryEntry populated
        private static IQueryable<SponsoredListingInvoice> WithIncludes(IQueryable<SponsoredListingInvoice> q) =>
            q.Include(i => i.DirectoryEntry) // ensures DirectoryEntry and its Name are populated
             .Include(i => i.SponsoredListing);         // optional, but often useful

        private static void CopyMutableFields(SponsoredListingInvoice target, SponsoredListingInvoice src)
        {
            // ⚠️ Do NOT touch target.SponsoredListingInvoiceId or target.InvoiceId or target.CreateDate here.

            // processor + request/response blobs
            target.PaymentProcessor = src.PaymentProcessor;
            target.InvoiceRequest = src.InvoiceRequest;
            target.InvoiceResponse = src.InvoiceResponse;
            target.PaymentResponse = src.PaymentResponse;
            target.ProcessorInvoiceId = src.ProcessorInvoiceId;

            // status + outcome
            target.PaymentStatus = src.PaymentStatus;
            target.PaidAmount = src.PaidAmount;
            target.OutcomeAmount = src.OutcomeAmount;
            target.PaidInCurrency = src.PaidInCurrency;

            // business data that can change during the flow
            target.Email = src.Email;
            target.ReservationGuid = src.ReservationGuid;
            target.ReferralCodeUsed = src.ReferralCodeUsed;
            target.SponsoredListingId = src.SponsoredListingId;

            // campaign / pricing (if you allow updating them)
            target.CampaignStartDate = src.CampaignStartDate;
            target.CampaignEndDate = src.CampaignEndDate;
            target.Amount = src.Amount;
            target.InvoiceDescription = src.InvoiceDescription;

            // scope
            target.SponsorshipType = src.SponsorshipType;
            target.SubCategoryId = src.SubCategoryId;
            target.CategoryId = src.CategoryId;
            target.DirectoryEntryId = src.DirectoryEntryId;

            // misc
            target.IpAddress = src.IpAddress;
            target.IsReminderSent = src.IsReminderSent;
        }
    }
}