using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DirectoryManager.Web.Controllers
{
    public abstract class BaseController : Controller
    {
        private readonly IMemoryCache cache;

        public BaseController(IMemoryCache cache)
        {
            this.cache = cache;
        }

        protected void ClearCachedItems() => SponsorCacheHelper.ClearSponsorCaches(this.cache);
    }
}
