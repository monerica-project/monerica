using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

public class RssController : Controller
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IDirectoryEntryRepository directoryEntryRepository;
    private readonly ISponsoredListingRepository sponsoredListingRepository;
    private readonly ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository;
    private readonly IRssFeedService rssFeedService;
    private readonly ICacheService cacheService;

    public RssController(
        IServiceScopeFactory scopeFactory,
        IDirectoryEntryRepository directoryEntryRepository,
        ISponsoredListingRepository sponsoredListingRepository,
        ISponsoredListingInvoiceRepository sponsoredListingInvoiceRepository,
        IRssFeedService rssFeedService,
        ICacheService cacheService)
    {
        this.scopeFactory = scopeFactory;
        this.directoryEntryRepository = directoryEntryRepository;
        this.sponsoredListingRepository = sponsoredListingRepository;
        this.sponsoredListingInvoiceRepository = sponsoredListingInvoiceRepository;
        this.rssFeedService = rssFeedService;
        this.cacheService = cacheService;
    }

    [HttpGet("rss/feed.xml")]
    public async Task<IActionResult> FeedXml()
    {
        var siteName = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteName);
        var siteLogoUrl = await this.cacheService.GetSnippetAsync(SiteConfigSetting.SiteLogoUrl);
        var feedLink = this.Url.Action("FeedXml", "Rss", null, this.Request.Scheme);

        if (string.IsNullOrEmpty(feedLink))
        {
            return this.BadRequest("Unable to generate feed link.");
        }

        var newestEntries = await this.directoryEntryRepository.GetNewestAdditions(IntegerConstants.MaxPageSize);
        var sponsoredEntries = await this.GetSponsoredEntriesWithRecentDatesAsync();

        var combinedEntries = newestEntries
            .Select(entry => new DirectoryEntryWrapper { DirectoryEntry = entry, IsSponsored = false })
            .Concat(sponsoredEntries)
            .Where(w => w?.DirectoryEntry != null) // safety
            .GroupBy(wrapper => wrapper!.DirectoryEntry.DirectoryEntryId)
            .Select(group => group.OrderByDescending(wrapper => wrapper!.DirectoryEntry.CreateDate).First() !)
            .OrderByDescending(wrapper => wrapper.DirectoryEntry.CreateDate)
            .ToList();

        var rssFeed = this.rssFeedService.GenerateRssFeed(
            combinedEntries,
            $"{siteName} - Newest and Sponsored Listings",
            feedLink,
            "The latest additions and sponsors in our directory.",
            siteLogoUrl);

        return this.Content(rssFeed.ToString(), "application/xml");
    }

    private async Task<IEnumerable<DirectoryEntryWrapper?>> GetSponsoredEntriesWithRecentDatesAsync()
    {
        var sponsoredListings = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync()
                                   ?? Enumerable.Empty<SponsoredListing>();

        var tasks = sponsoredListings.Select(async sponsoredListing =>
        {
            // Skip if the related entry isn't loaded/available
            var entry = sponsoredListing.DirectoryEntry;
            if (entry == null)
            {
                return (DirectoryEntryWrapper?)null;
            }

            using var scope = this.scopeFactory.CreateScope();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<ISponsoredListingInvoiceRepository>();

            // Most recent invoice (may be null)
            var invoice = await invoiceRepo.GetByIdAsync(sponsoredListing.SponsoredListingInvoiceId);

            // Safely pick the most relevant "recent date"
            var invoiceCreate = invoice?.CreateDate ?? DateTime.MinValue;
            var invoiceCampaign = invoice?.CampaignStartDate ?? DateTime.MinValue;
            var sponsoredUpdated = sponsoredListing.UpdateDate ?? DateTime.MinValue;

            var latestDate = new[] { invoiceCreate, sponsoredUpdated, invoiceCampaign }.Max();

            // Wrap with a shallow copy, but with a safe CreateDate
            return new DirectoryEntryWrapper
            {
                DirectoryEntry = new DirectoryEntry
                {
                    DirectoryEntryKey = entry.DirectoryEntryKey,
                    DirectoryEntryId = entry.DirectoryEntryId,
                    Name = entry.Name,
                    Link = entry.Link,
                    LinkA = entry.LinkA,
                    SubCategory = entry.SubCategory,
                    DirectoryStatus = entry.DirectoryStatus,
                    Description = entry.Description,
                    Location = entry.Location,
                    Processor = entry.Processor,
                    Note = entry.Note,
                    Contact = entry.Contact,
                    CreateDate = latestDate, // <- use latestDate, not invoice.CreateDate
                },
                IsSponsored = true
            };
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null) !;
    }
}
