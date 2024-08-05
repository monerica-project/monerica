using System.Xml.Linq;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Services.Interfaces
{
    public interface IRssFeedService
    {
        XDocument GenerateRssFeed(IEnumerable<DirectoryEntry> directoryEntries, string feedTitle, string feedLink, string feedDescription);
    }
}