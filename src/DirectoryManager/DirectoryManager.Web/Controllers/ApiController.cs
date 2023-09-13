using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    public class ApiController : ControllerBase
    {
        private readonly IDirectoryEntryRepository _entryRepository;
        public ApiController(IDirectoryEntryRepository entryRepository)
        {
            _entryRepository = entryRepository;
        }


        [HttpGet("api/all")]
        public async Task<IActionResult> GetAllEntitiesAndProperties()
        {
            var entities = await _entryRepository.GetAllEntitiesAndPropertiesAsync();
            return Ok(entities);
        }

    }
}
