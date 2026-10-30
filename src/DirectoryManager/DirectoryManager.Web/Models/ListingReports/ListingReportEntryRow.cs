using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.ListingReports
{
    public class ListingReportEntryRow
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public DirectoryStatus CurrentStatus { get; set; }
    }
}
