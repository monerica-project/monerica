using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISponsoredListingInvoiceRepository
    {
        Task<SponsoredListingInvoice?> GetByIdAsync(int sponsoredListingInvoiceId);
        Task<SponsoredListingInvoice?> GetByInvoiceIdAsync(Guid invoiceId);
        Task<SponsoredListingInvoice?> GetByReservationGuidAsync(Guid invoiceId);
        Task<SponsoredListingInvoice> GetByInvoiceProcessorIdAsync(string processorInvoiceId);
        Task<IEnumerable<SponsoredListingInvoice>> GetAllAsync();
        Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageAsync(int page, int pageSize);
        Task<(IEnumerable<SponsoredListingInvoice>, int)> GetPageByTypeAsync(int page, int pageSize, PaymentStatus paymentStatus);
        Task<SponsoredListingInvoice> CreateAsync(SponsoredListingInvoice invoice);
        Task<bool> UpdateAsync(SponsoredListingInvoice invoice);
        Task<InvoiceTotalsResult> GetTotalsPaidAsync(DateTime startDate, DateTime endDate);
        Task DeleteAsync(int sponsoredListingInvoiceId);
        DateTime? GetLastPaidInvoiceUpdateDate();
        Task<(IEnumerable<SponsoredListingInvoice> Invoices, int TotalCount)> GetInvoicesForDirectoryEntryAsync(int directoryEntryId, int page, int pageSize);
        Task<bool> HasAnyPaidInvoiceForDirectoryEntryAsync(int directoryEntryId, int excludeSponsoredListingInvoiceId, CancellationToken ct = default);
        Task<(IEnumerable<SponsoredListingInvoice> Invoices, int TotalCount)>
        GetInvoicesForDirectoryEntryInWindowAsync(
        int directoryEntryId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        SponsorshipType? sponsorshipType,
        bool paidOnly,
        bool useCampaignOverlap,
        int page,
        int pageSize);
        Task<List<AdvertiserWindowStat>> GetAdvertiserWindowStatsAsync(
        DateTime windowStartDate,
        DateTime windowEndDate,
        SponsorshipType? sponsorshipType = null,
        bool paidOnly = true);
        Task<List<AdvertiserWindowSum>> GetAdvertiserInvoiceWindowSumsAsync(
        DateTime windowStartUtc,
        DateTime windowEndOpenUtc,
        SponsorshipType? sponsorshipType = null);

        IAsyncEnumerable<AccountantRow> StreamPaidForAccountantAsync(DateTime startUtc, DateTime endUtc, SponsorshipType? sponsorshipType, CancellationToken ct = default);
        Task<SponsoredListingInvoice?> GetByProcessorInvoiceIdAsync(string ipnProcessorId);
        Task<List<RecentPaidPurchaseDto>> GetRecentPaidActivePurchasesAsync(int take);

        Task<List<RecentPaidPurchaseDto>> GetRecentPaidByTypeAsync(
            SponsorshipType sponsorshipType, int take, IReadOnlyCollection<DirectoryStatus> allowedStatuses);

        /// <summary>
        /// True when this exact placement (a category page for a category sponsor, a subcategory
        /// page for a subcategory sponsor) has paid history, using the placement saved on each invoice.
        /// </summary>
        Task<bool> HasPaidPurchasesForPlacementAsync(SponsorshipType sponsorshipType, int placementId);

        DateTime? GetLastPaidInvoiceCreateDate();

        DateTime? GetLastPendingInvoiceCreateDate();

        /// <summary>
        /// Checkouts that need a look: payment seen but not confirmed (Pending), too little sent
        /// (UnderPaid), and too much sent in the last 30 days (OverPaid30d, already activated).
        /// </summary>
        (int Pending, int UnderPaid, int OverPaid30d) GetPaymentAttentionCounts();
    }
}