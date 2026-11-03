using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    /// <summary>Queries behind the admin revenue-opportunity reports.</summary>
    public interface IRevenueOpportunityRepository
    {
        /// <summary>Non-test invoices created since <paramref name="since"/> (any status), with their listing.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<List<SponsoredListingInvoice>> GetInvoicesSinceAsync(DateTime since);

        /// <summary>Sponsorships whose campaign ends on or after <paramref name="since"/>, with listing and invoice.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<List<SponsoredListing>> GetSponsorshipsEndingSinceAsync(DateTime since);

        /// <summary>Listings that have ever paid for a sponsorship.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<HashSet<int>> GetEverPaidEntryIdsAsync();

        /// <summary>Listing page views by listing key.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<Dictionary<string, ListingPageView>> GetListingPageViewsAsync();

        /// <summary>Marks one invoice as test (or not).</summary>
        /// <returns>The invoice's email, or null when not found.</returns>
        Task<string?> SetInvoiceTestAsync(int invoiceId, bool isTest);

        /// <summary>Marks every invoice with this email as test (or not).</summary>
        /// <returns>How many invoices changed.</returns>
        Task<int> SetTestByEmailAsync(string email, bool isTest);

        /// <summary>True when an earlier checkout with this email was marked as test.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<bool> IsTestEmailAsync(string? email);
    }
}
