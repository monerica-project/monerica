using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
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
            return this.Ok(entities);
        }
    }
}