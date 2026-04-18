namespace DirectoryManager.Data.Models.TransferModels
{
    public class MonthlyAuditReport
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Additions { get; set; }
        public int Removals { get; set; }
        public int ActiveCount { get; set; }
    }
}