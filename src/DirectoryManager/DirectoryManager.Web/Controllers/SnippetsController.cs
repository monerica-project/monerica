using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    [ApiController]
    public class SnippetsController : Controller
    {
        private readonly ISponsoredListingRepository sponsoredListingRepository;

        public SnippetsController(ISponsoredListingRepository sponsoredListingRepository)
        {
            this.sponsoredListingRepository = sponsoredListingRepository;
        }

        // GET /snippets/ads
        [HttpGet("/snippets/ads")]
        [Produces("text/html")]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, NoStore = false)]
        [AllowAnonymous]
        public async Task<IActionResult> AdsAsync()
        {
            // Enable cross-domain fetch+inject if you need it:
            this.Response.Headers["Access-Control-Allow-Origin"] = "*";
            var sponsors = await this.sponsoredListingRepository.GetActiveSponsorsByTypeAsync(Data.Enums.SponsorshipType.MainSponsor);

            // IMPORTANT: PartialView to avoid site layout
            return this.PartialView("_AdsSnippet", sponsors);
        }
    }
}
