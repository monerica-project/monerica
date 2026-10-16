using System.Text;
using System.Xml.Linq;
using DirectoryManager.Common.Helpers;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class RssFeedService : IRssFeedService
    {
        public XDocument GenerateRssFeed(
            IEnumerable<DirectoryEntryWrapper> directoryEntries,
            string feedTitle,
            string feedLink,
            string feedDescription,
            string? logoUrl = null)
        {
            var channelElements = new List<XElement>
            {
                new XElement("title", feedTitle),
                new XElement("link", feedLink),
                new XElement("description", feedDescription)
            };

            // Conditionally add the <image> element if a logo URL is provided
            if (!string.IsNullOrWhiteSpace(logoUrl))
            {
                channelElements.Add(
                    new XElement(
                        "image",
                        new XAttribute("url", logoUrl)));
            }

            var siteOrigin = GetOrigin(feedLink);

            // Add directory entries
            channelElements.AddRange(
                directoryEntries.Select(entryWrapper =>
                    new XElement(
                        "item",
                        new XElement("title", GetFormattedTitle(entryWrapper)),
                        new XElement("link", GetItemLink(entryWrapper, siteOrigin)),
                        new XElement("description", this.FormatDescription(entryWrapper.DirectoryEntry)),
                        new XElement("pubDate", entryWrapper.DirectoryEntry.CreateDate.ToString("R")))));

            var rss = new XElement(
                "rss",
                new XAttribute("version", "2.0"),
                new XElement("channel", channelElements));

            return new XDocument(rss);
        }

        // XElement escapes text itself, so the status marks must be the real characters — an
        // HTML entity like "&#x274C;" would be double-encoded and show up literally in readers.
        internal static string GetFormattedTitle(DirectoryEntryWrapper wrapper)
        {
            var baseTitle = wrapper.DirectoryEntry.DirectoryStatus switch
            {
                Data.Enums.DirectoryStatus.Scam => $"❌ {wrapper.GetName()}",
                Data.Enums.DirectoryStatus.Verified => $"✅ {wrapper.GetName()}",
                Data.Enums.DirectoryStatus.Questionable => $"❓ {wrapper.GetName()}",
                _ => wrapper.GetName()
            };

            // Append "(sponsored)" if it is sponsored
            return wrapper.IsSponsored ? $"{baseTitle} (sponsored)" : baseTitle;
        }

        // Scam listings must never link straight to the scam site — send readers to the
        // listing's profile page (which carries the warning) instead.
        internal static string GetItemLink(DirectoryEntryWrapper wrapper, string siteOrigin)
        {
            var entry = wrapper.DirectoryEntry;
            if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
            {
                return string.IsNullOrWhiteSpace(entry.DirectoryEntryKey)
                    ? siteOrigin
                    : siteOrigin.TrimEnd('/') + UrlBuilder.ListingPath(entry.DirectoryEntryKey);
            }

            return wrapper.GetLink();
        }

        private static string GetOrigin(string feedLink)
        {
            return Uri.TryCreate(feedLink, UriKind.Absolute, out var uri)
                ? uri.GetLeftPart(UriPartial.Authority)
                : string.Empty;
        }

        private string FormatDescription(Data.Models.DirectoryEntry entry)
        {
            var descriptionBuilder = new StringBuilder();

            var categoryInfo = entry.SubCategory?.Category != null
                ? CategoryFormatter.Format(entry.SubCategory.Category.Name, entry.SubCategory.Name)
                : entry.SubCategory?.Name ?? "Uncategorized";

            descriptionBuilder.Append(categoryInfo).Append(" : ").Append(entry.Description);

            if (!string.IsNullOrWhiteSpace(entry.Note))
            {
                descriptionBuilder.Append(" - Note: ").Append(entry.Note);
            }

            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                descriptionBuilder.Append(" - Location: ").Append(entry.Location);
            }

            if (!string.IsNullOrWhiteSpace(entry.Processor))
            {
                descriptionBuilder.Append(" - Processor: ").Append(entry.Processor);
            }

            return descriptionBuilder.ToString();
        }
    }
}
