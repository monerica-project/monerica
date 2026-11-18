using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Extensions
{
    public static class StaticPublishControllerExtensions
    {
        /// <summary>The request's <see cref="IStaticPublishService"/> (publish-on-change queue).</summary>
        public static IStaticPublishService StaticPublish(this ControllerBase controller) =>
            controller.HttpContext.RequestServices.GetRequiredService<IStaticPublishService>();
    }
}
