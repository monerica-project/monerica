using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// The 1x1 image that counts sponsor-interest page views (see <see cref="SponsorInterestTracker.SetBeacon"/>).
    /// Crawlers that pretend to be browsers fetch the page HTML but not its images, so they are not counted.
    /// Always returns the image, counted or not, and never caches.
    /// </summary>
    public class SponsorInterestBeaconController : Controller
    {
        // Smallest transparent GIF.
        private static readonly byte[] Gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        [HttpGet(SponsorInterestTracker.BeaconPath)]
        public async Task<IActionResult> Seen([FromQuery] int e, [FromQuery] int t, [FromQuery] int s, [FromQuery] int? p)
        {
            this.Response.Headers.CacheControl = "no-store, max-age=0";
            this.Response.Headers["X-Robots-Tag"] = "noindex";

            var step = (SponsorInterestStep)s;
            var type = (SponsorshipType)t;
            if (e > 0
                && Enum.IsDefined(step) && SponsorInterestTracker.IsBeaconStep(step)
                && Enum.IsDefined(type)
                && SponsorInterestTracker.LooksLikeImageLoad(this.Request.Headers["Sec-Fetch-Dest"].ToString(), this.Request.Headers["Sec-Fetch-Site"].ToString()))
            {
                await SponsorInterestTracker.LogAsync(this.HttpContext, e, type, p, step);
            }

            return this.File(Gif, "image/gif");
        }
    }
}
