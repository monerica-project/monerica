using System.Text;
using System.Xml.Linq;
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

            // Add directory entries
            channelElements.AddRange(
                directoryEntries.Select(entryWrapper =>
                    new XElement(
                        "item",
                        new XElement("title", this.GetFormattedTitle(entryWrapper)),
                        new XElement("link", entryWrapper.GetLink()),
                        new XElement("description", this.FormatDescription(entryWrapper.DirectoryEntry)),
                        new XElement("pubDate", entryWrapper.DirectoryEntry.CreateDate.ToString("R")))));

            var rss = new XElement(
                "rss",
                new XAttribute("version", "2.0"),
                new XElement("channel", channelElements));

            return new XDocument(rss);
        }

        private string GetFormattedTitle(DirectoryEntryWrapper wrapper)
        {
            var baseTitle = wrapper.DirectoryEntry.DirectoryStatus switch
            {
                Data.Enums.DirectoryStatus.Scam => $"&#x274C; {wrapper.GetName()}",
                Data.Enums.DirectoryStatus.Verified => $"&#x2705; {wrapper.GetName()}",
                Data.Enums.DirectoryStatus.Questionable => $"&#x2753; {wrapper.GetName()}",
                _ => wrapper.GetName()
            };

            // Append "(sponsored)" if it is sponsored
            return wrapper.IsSponsored ? $"{baseTitle} (sponsored)" : baseTitle;
        }

        private string FormatDescription(Data.Models.DirectoryEntry entry)
        {
            var descriptionBuilder = new StringBuilder();

            var categoryInfo = entry.SubCategory?.Category != null
                ? $"{entry.SubCategory.Category.Name} > {entry.SubCategory.Name}"
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

            if (!string.IsNullOrWhiteSpace(entry.Contact))
            {
                descriptionBuilder.Append(" - Contact: ").Append(entry.Contact);
            }

            return descriptionBuilder.ToString();
        }
    }
}
