namespace DirectoryManager.Web.Models.Reports
{
    public class BreakdownRow
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}
