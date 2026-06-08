using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Models.API;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class ApiController : BaseController
    {
        private readonly IDirectoryEntryRepository entryRepository;

        public ApiController(
            IDirectoryEntryRepository entryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService,
            IMemoryCache cache)
            : base(trafficLogRepository, userAgentCacheService, cache)
        {
            this.entryRepository = entryRepository;
        }

        [HttpGet("api/all")]
        [ResponseCache(Duration = IntegerConstants.CacheDurationSeconds)]
        public async Task<IActionResult> GetAllEntitiesAndProperties()
        {
            var entities = await this.entryRepository.GetAllEntitiesAndPropertiesAsync();

            var result = entities
                .Where(e => e.DirectoryStatus != DirectoryStatus.Removed
                         && e.DirectoryStatus != DirectoryStatus.Unknown)
                .Select(ToPublicDto)
                .ToList();

            return this.Ok(result);
        }

        private static PublicDirectoryEntryDto ToPublicDto(DirectoryManager.Data.Models.DirectoryEntry e)
        {
            return new PublicDirectoryEntryDto
            {
                DirectoryEntryId = e.DirectoryEntryId,
                Name = e.Name,
                DirectoryEntryKey = e.DirectoryEntryKey,
                Link = e.Link,
                Link2 = e.Link2,
                Link3 = e.Link3,
                Description = e.Description,
                Note = e.Note,
                Location = e.Location,
                Processor = e.Processor,
                CountryCode = e.CountryCode,
                DirectoryStatus = e.DirectoryStatus,
                DirectoryBadge = e.DirectoryBadge,
                FoundedDate = e.FoundedDate,
                ProofLink = e.ProofLink,
                VideoLink = e.VideoLink,
                ReviewsDisabled = e.ReviewsDisabled,
                PgpKey = e.PgpKey,
                CreateDate = e.CreateDate,
                UpdateDate = e.UpdateDate,
                SubCategory = e.SubCategory == null
                    ? null
                    : new PublicSubcategoryDto
                    {
                        Name = e.SubCategory.Name,
                        SubCategoryKey = e.SubCategory.SubCategoryKey,
                        Category = e.SubCategory.Category == null
                            ? null
                            : new PublicCategoryDto
                            {
                                Name = e.SubCategory.Category.Name,
                                CategoryKey = e.SubCategory.Category.CategoryKey,
                            },
                    },
                Tags = e.EntryTags == null
                    ? new List<string>()
                    : e.EntryTags
                        .Where(et => et.Tag != null)
                        .Select(et => et.Tag.Name)
                        .OrderBy(n => n)
                        .ToList(),
            };
        }
    }
}