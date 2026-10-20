using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DirectoryManager.Web.Extensions
{
    public static class ControllerPageMetadataExtensions
    {
        /// <summary>This request's typed page settings (title, meta tags, canonical URL).</summary>
        public static PageMetadata PageMeta(this ControllerBase controller) =>
            PageMetadata.For(controller.ControllerContext?.HttpContext);
    }
}
