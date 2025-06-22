using System.Xml.Linq;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface IRssFeedService
    {
        XDocument GenerateRssFeed(
            IEnumerable<DirectoryEntryWrapper> directoryEntries,
            string feedTitle,
            string feedLink,
            string feedDescription,
            string logoUrl);
    }
}