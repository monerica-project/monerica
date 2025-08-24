namespace DirectoryManager.Data.Models.TransferModels
{
    public class WeeklySearchCount
    {
        /// <summary>UTC Monday (00:00) starting this ISO week.</summary>
        public DateTime WeekStartUtc { get; set; }

        /// <summary>Total searches in that week.</summary>
        public int Count { get; set; }
    }
}