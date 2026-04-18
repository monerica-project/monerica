using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [AllowAnonymous]
    [Route("advertisers")]
    public class AdvertisersController : Controller
    {
        private readonly ISponsoredListingRepository sponsoredListingRepository;
        private readonly IDirectoryEntryRepository directoryEntryRepository;

        public AdvertisersController(
            ISponsoredListingRepository sponsoredListingRepository,
            IDirectoryEntryRepository directoryEntryRepository)
        {
            this.sponsoredListingRepository = sponsoredListingRepository;
            this.directoryEntryRepository = directoryEntryRepository;
        }

        // GET /advertisers
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var listings = await this.sponsoredListingRepository.GetAllAsync();

            var advertisedEntryIds = listings
                .Where(l => l.DirectoryEntryId > 0)
                .Select(l => l.DirectoryEntryId)
                .Distinct()
                .ToHashSet();

            var allEntries = await this.directoryEntryRepository.GetAllAsync();

            var model = allEntries
                .Where(e => advertisedEntryIds.Contains(e.DirectoryEntryId)
                            && e.DirectoryStatus != DirectoryStatus.Removed)
                .OrderBy(e => e.Name)
                .Select(e => new AdvertisedEntryVm
                {
                    DirectoryEntryId = e.DirectoryEntryId,
                    Name = e.Name
                })
                .ToList();

            return this.View(model);
        }

        public class AdvertisedEntryVm
        {
            public int DirectoryEntryId { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}