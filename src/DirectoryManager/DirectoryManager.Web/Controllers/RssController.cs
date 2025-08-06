using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
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
        var siteName = this.cacheService.GetSnippet(SiteConfigSetting.SiteName);
        var siteLogoUrl = this.cacheService.GetSnippet(SiteConfigSetting.SiteLogoUrl);
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
            .GroupBy(wrapper => wrapper.DirectoryEntry.DirectoryEntryId)
            .Select(group => group.OrderByDescending(wrapper => wrapper.DirectoryEntry.CreateDate).First())
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

    private async Task<IEnumerable<DirectoryEntryWrapper>> GetSponsoredEntriesWithRecentDatesAsync()
    {
        var sponsoredListings = await this.sponsoredListingRepository.GetAllActiveSponsorsAsync();

        var tasks = sponsoredListings.Select(async sponsoredListing =>
        {
            using var scope = this.scopeFactory.CreateScope();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<ISponsoredListingInvoiceRepository>();

            // Fetch the most recent invoice associated with this listing
            var invoice = await invoiceRepo.GetByIdAsync(sponsoredListing.SponsoredListingInvoiceId);
            var invoiceCampaignDate = (invoice == null) ? DateTime.MinValue : invoice.CampaignStartDate;

            var latestDate = new[]
            {
                invoice.CreateDate,
                sponsoredListing.UpdateDate ?? DateTime.MinValue,
                invoiceCampaignDate
            }.Max();

            // Wrap the DirectoryEntry with correct pubDate
            return new DirectoryEntryWrapper
            {
                DirectoryEntry = new DirectoryEntry
                {
                    DirectoryEntryKey = sponsoredListing.DirectoryEntry.DirectoryEntryKey,
                    DirectoryEntryId = sponsoredListing.DirectoryEntry.DirectoryEntryId,
                    Name = sponsoredListing.DirectoryEntry.Name,
                    Link = sponsoredListing.DirectoryEntry.Link,
                    LinkA = sponsoredListing.DirectoryEntry.LinkA,
                    SubCategory = sponsoredListing.DirectoryEntry.SubCategory,
                    DirectoryStatus = sponsoredListing.DirectoryEntry.DirectoryStatus,
                    Description = sponsoredListing.DirectoryEntry.Description,
                    Location = sponsoredListing.DirectoryEntry.Location,
                    Processor = sponsoredListing.DirectoryEntry.Processor,
                    Note = sponsoredListing.DirectoryEntry.Note,
                    Contact = sponsoredListing.DirectoryEntry.Contact,
                    CreateDate = invoice.CreateDate,
                },
                IsSponsored = true
            };
        });

        return await Task.WhenAll(tasks);
    }
}
