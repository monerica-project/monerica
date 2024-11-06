using System.Xml.Linq;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    public class RssFeedService : IRssFeedService
    {
        public XDocument GenerateRssFeed(IEnumerable<DirectoryEntry> directoryEntries, string feedTitle, string feedLink, string feedDescription)
        {
            var rss = new XElement(
                "rss",
                new XAttribute("version", "2.0"),
                new XElement(
                    "channel",
                    new XElement("title", feedTitle),
                    new XElement("link", feedLink),
                    new XElement("description", feedDescription),
                    directoryEntries.Select(entry =>
                        new XElement(
                            "item",
                            new XElement(
                                "title",
                                entry.DirectoryStatus == Data.Enums.DirectoryStatus.Scam
                                    ? $"{Data.Enums.DirectoryStatus.Scam}! - {entry.Name}"
                                    : entry.Name),
                            new XElement("link", string.IsNullOrEmpty(entry.LinkA) ? entry.Link : entry.LinkA),
                            new XElement(
                                "description",
                                this.FormatDescription(entry)),
                            new XElement("pubDate", entry.CreateDate.ToString("R"))))));

            return new XDocument(rss);
        }

        private string FormatDescription(DirectoryEntry entry)
        {
            // Retrieve category and subcategory info
            var categoryInfo = entry.SubCategory?.Category != null
                ? $"{entry.SubCategory.Category.Name} > {entry.SubCategory.Name}"
                : entry.SubCategory?.Name ?? "Uncategorized";

            // Build the description with Category > Subcategory : Description - Note format
            var formattedDescription = $"{categoryInfo} : {entry.Description}";

            // Append note if it exists
            if (!string.IsNullOrWhiteSpace(entry.Note))
            {
                formattedDescription += $" - Note: {entry.Note}";
            }

            // Append additional fields if they are not null or empty
            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                formattedDescription += $" - Location: {entry.Location}";
            }
            if (!string.IsNullOrWhiteSpace(entry.Processor))
            {
                formattedDescription += $" - Processor: {entry.Processor}";
            }
            if (!string.IsNullOrWhiteSpace(entry.Contact))
            {
                formattedDescription += $" - Contact: {entry.Contact}";
            }

            return formattedDescription;
        }
    }
}