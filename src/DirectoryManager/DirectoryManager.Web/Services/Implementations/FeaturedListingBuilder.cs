using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class FeaturedListingBuilder : IFeaturedListingBuilder
    {
        private readonly IDirectoryEntryRepository entryRepository;
        private readonly IDirectoryEntryReviewRepository reviewRepository;
        private readonly IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepository;
        private readonly ICacheService cacheService;

        public FeaturedListingBuilder(
            IDirectoryEntryRepository entryRepository,
            IDirectoryEntryReviewRepository reviewRepository,
            IDirectoryEntryAdditionalSubcategoryRepository additionalSubcategoryRepository,
            ICacheService cacheService)
        {
            this.entryRepository = entryRepository;
            this.reviewRepository = reviewRepository;
            this.additionalSubcategoryRepository = additionalSubcategoryRepository;
            this.cacheService = cacheService;
        }

        public async Task<DirectoryEntryViewModel?> BuildAsync(int? directoryEntryId, int? subCategoryId = null)
        {
            if (directoryEntryId is null)
            {
                return null;
            }

            var entry = await this.entryRepository.GetByIdAsync(directoryEntryId.Value);
            if (entry == null || !FeaturedListingRules.IsFeaturable(entry.DirectoryStatus))
            {
                return null;
            }

            // Featured through an additional subcategory: show that subcategory's name/links/description.
            if (subCategoryId.HasValue && entry.SubCategoryId != subCategoryId.Value)
            {
                var alts = await this.additionalSubcategoryRepository.GetByDirectoryEntryIdAsync(entry.DirectoryEntryId);
                var altHere = alts.FirstOrDefault(a => a.SubCategoryId == subCategoryId.Value);
                if (altHere != null)
                {
                    entry = altHere.ApplyTo(entry);
                }
            }

            var ratings = await this.reviewRepository.GetRatingSummariesAsync(new[] { entry.DirectoryEntryId });
            ratings.TryGetValue(entry.DirectoryEntryId, out var rating);
            bool hasRating = rating != null && rating.ReviewCount > 0;

            return new DirectoryEntryViewModel
            {
                DirectoryEntryId = entry.DirectoryEntryId,
                DirectoryEntryKey = entry.DirectoryEntryKey,
                Name = entry.Name,
                Description = entry.Description,
                Note = entry.Note,
                DirectoryStatus = entry.DirectoryStatus,
                CountryCode = entry.CountryCode,
                Location = entry.Location,
                Processor = entry.Processor,
                SubCategoryId = entry.SubCategoryId,
                Link = entry.Link,
                LinkA = entry.LinkA,
                Link2 = entry.Link2,
                Link2A = entry.Link2A,
                Link3 = entry.Link3,
                Link3A = entry.Link3A,
                CreateDate = entry.CreateDate,
                UpdateDate = entry.UpdateDate,
                DateOption = DateDisplayOption.NotDisplayed,
                ItemPath = UrlBuilder.ListingPath(entry.DirectoryEntryKey),
                Link2Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link2Name),
                Link3Name = await this.cacheService.GetSnippetAsync(SiteConfigSetting.Link3Name),

                // Same compact box as a sponsor, but unpaid: keeps its affiliate link.
                IsFeatured = true,
                IsSponsored = false,
                DisplayAsSponsoredItem = false,
                IsSubCategorySponsor = false,
                SponsorInlineHeader = true,
                SkipLinksBlock = true,
                ShowProfileLink = true,
                LinkType = LinkType.Direct,

                AverageRating = hasRating ? rating!.AvgRating : null,
                ReviewCount = hasRating ? rating!.ReviewCount : null,
            };
        }
    }
}
