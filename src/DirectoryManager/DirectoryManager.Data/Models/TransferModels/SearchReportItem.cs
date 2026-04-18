namespace DirectoryManager.Data.Models.TransferModels
{
    public class SearchReportItem
    {
        public string Term { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime FirstSearched { get; set; }
        public DateTime LastSearched { get; set; }
        public double Percentage { get; set; }
    }
}