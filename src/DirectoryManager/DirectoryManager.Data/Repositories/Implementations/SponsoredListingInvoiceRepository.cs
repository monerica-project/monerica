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
    }
}