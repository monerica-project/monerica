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
                                    ? $"{Data.Enums.DirectoryStatus.Scam.ToString()}! - {entry.Name}"
                                    : entry.Name),
                            new XElement("link", entry.Link),
                            new XElement("description",  entry.Description),
                            new XElement("pubDate", entry.CreateDate.ToString("R"))))));

            return new XDocument(rss);
        }
    }
}
