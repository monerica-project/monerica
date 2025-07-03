namespace DirectoryManager.Web.Models.Reports
{
    public class AdvertiserBreakdownRow
    {
        public int DirectoryEntryId { get; set; }
        public string DirectoryEntryName { get; set; } = "";
        public decimal Revenue { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}