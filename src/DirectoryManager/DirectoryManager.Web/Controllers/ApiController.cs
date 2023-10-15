using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class ApiController : BaseController
    {
        private readonly IDirectoryEntryRepository entryRepository;
        public ApiController(
            IDirectoryEntryRepository entryRepository,
            ITrafficLogRepository trafficLogRepository,
            IUserAgentCacheService userAgentCacheService)
            : base(trafficLogRepository, userAgentCacheService)
        {
            this.entryRepository = entryRepository;
        }

        [HttpGet("api/all")]
        [ResponseCache(Duration = 3600)] // Cache the response for 1 hour (3600 seconds)
        public async Task<IActionResult> GetAllEntitiesAndProperties()
        {
            var entities = await this.entryRepository.GetAllEntitiesAndPropertiesAsync();
            return this.Ok(entities);
        }
    }
}