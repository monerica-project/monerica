namespace DirectoryManager.Web.Models.Reviews
{
    public class RaffleFlowState
    {
        public int ReviewId { get; set; }
        public string Fingerprint { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }
}
