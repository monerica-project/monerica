using DirectoryManager.Data.Enums;
using Microsoft.AspNetCore.Html;

namespace DirectoryManager.Web.Helpers
{
    public static class DirectoryStatusExtensions
    {
        /// <summary>
        /// Returns the HTML entity icon for a DirectoryStatus.
        /// </summary>
        /// <returns>HTML hex</returns>
        public static IHtmlContent ToHtmlIcon(this DirectoryStatus status)
        {
            // map statuses to their hex entity
            string entity = status switch
            {
                DirectoryStatus.Verified => "&#9989;",  // ✅
                DirectoryStatus.Admitted => "",
                DirectoryStatus.Questionable => "&#10067;", // ❓
                DirectoryStatus.Scam => "&#10060;", // ❌
                _ => string.Empty
            };
            return new HtmlString(entity);
        }
    }
}
