using System.Text.Json;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Html;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Generates Schema.org JSON-LD breadcrumb markup.
    /// </summary>
    public static class BreadcrumbJsonHelper
    {
        /// <summary>
        /// Builds and returns a <script type="application/ld+json"> tag containing the BreadcrumbList JSON-LD.
        /// </summary>
        /// <param name="items">Sequence of BreadcrumbItem instances.</param>
        /// <returns>IHtmlContent for injection in a Razor view.</returns>
        public static IHtmlContent GenerateBreadcrumbJson(IEnumerable<BreadcrumbItem> items)
        {
            if (items == null || !items.Any())
            {
                return new HtmlString(string.Empty);
            }

            // Build each ListItem as a dictionary to preserve "@" keys
            var listItems = items.Select(i => new Dictionary<string, object>
            {
                ["@type"] = "ListItem",
                ["position"] = i.Position,
                ["name"] = i.Name,
                ["item"] = i.Url
            }).ToList();

            // Top-level JSON-LD object
            var jsonLd = new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = listItems
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DictionaryKeyPolicy = null,
                WriteIndented = false
            };

            var json = JsonSerializer.Serialize(jsonLd, options);
            var scriptTag = $"<script type=\"application/ld+json\">{json}</script>";

            return new HtmlString(scriptTag);
        }
    }
}