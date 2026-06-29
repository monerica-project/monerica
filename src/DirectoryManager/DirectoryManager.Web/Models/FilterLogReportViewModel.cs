namespace DirectoryManager.Web.Models
{
    public class FilterLogReportViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int TotalEvents { get; set; }

        public IReadOnlyList<FilterReportItem> Categories { get; set; } = new List<FilterReportItem>();

        public IReadOnlyList<FilterReportItem> Subcategories { get; set; } = new List<FilterReportItem>();

        public IReadOnlyList<FilterReportItem> Tags { get; set; } = new List<FilterReportItem>();

        public IReadOnlyList<FilterReportItem> Terms { get; set; } = new List<FilterReportItem>();

        public IReadOnlyList<FilterReportItem> Countries { get; set; } = new List<FilterReportItem>();

        public int VideoCount { get; set; }

        public int TorCount { get; set; }

        public int I2pCount { get; set; }
    }
}
