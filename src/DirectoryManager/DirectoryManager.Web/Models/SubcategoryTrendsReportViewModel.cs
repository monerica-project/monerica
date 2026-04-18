namespace DirectoryManager.Web.Models
{
    public class SubcategoryTrendsReportViewModel
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public List<SubcategoryTrendItem> TopGrowth { get; set; } = new ();
        public List<SubcategoryTrendItem> TopDecline { get; set; } = new ();
    }
}
