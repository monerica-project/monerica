using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class RevenueOpportunityRepository : IRevenueOpportunityRepository
    {
        private readonly IApplicationDbContext context;

        public RevenueOpportunityRepository(IApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<SponsoredListingInvoice>> GetInvoicesSinceAsync(DateTime since)
        {
            return await this.context.SponsoredListingInvoices
                .AsNoTracking()
                .Include(i => i.DirectoryEntry)
                .Where(i => !i.IsTest && i.CreateDate >= since)
                .ToListAsync();
        }

        public async Task<List<SponsoredListing>> GetSponsorshipsEndingSinceAsync(DateTime since)
        {
            return await this.context.SponsoredListings
                .AsNoTracking()
                .Include(s => s.DirectoryEntry)
                .Include(s => s.SponsoredListingInvoice)
                .Where(s => s.CampaignEndDate >= since)
                .ToListAsync();
        }

        public async Task<HashSet<int>> GetEverPaidEntryIdsAsync()
        {
            var ids = await this.context.SponsoredListingInvoices
                .Where(i => i.PaymentStatus == PaymentStatus.Paid || i.PaymentStatus == PaymentStatus.OverPayment)
                .Select(i => i.DirectoryEntryId)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<Dictionary<string, ListingPageView>> GetListingPageViewsAsync()
        {
            return await this.context.ListingPageViews
                .AsNoTracking()
                .ToDictionaryAsync(v => v.DirectoryEntryKey, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<string?> SetInvoiceTestAsync(int invoiceId, bool isTest)
        {
            var inv = await this.context.SponsoredListingInvoices.FirstOrDefaultAsync(i => i.SponsoredListingInvoiceId == invoiceId);
            if (inv == null)
            {
                return null;
            }

            inv.IsTest = isTest;
            await this.context.SaveChangesAsync();
            return inv.Email ?? string.Empty;
        }

        public async Task<int> SetTestByEmailAsync(string email, bool isTest)
        {
            var e = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (e.Length == 0)
            {
                return 0;
            }

            var rows = await this.context.SponsoredListingInvoices
                .Where(i => i.Email != null && i.Email.ToLower() == e && i.IsTest != isTest)
                .ToListAsync();
            rows.ForEach(i => i.IsTest = isTest);
            await this.context.SaveChangesAsync();
            return rows.Count;
        }

        public async Task<bool> IsTestEmailAsync(string? email)
        {
            var e = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (e.Length == 0)
            {
                return false;
            }

            return await this.context.SponsoredListingInvoices
                .AnyAsync(i => i.IsTest && i.Email != null && i.Email.ToLower() == e);
        }
    }
}
