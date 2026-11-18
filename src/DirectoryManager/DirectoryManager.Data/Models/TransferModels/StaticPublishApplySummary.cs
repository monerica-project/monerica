namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>What applying one batch of publisher results did to the queue.</summary>
    public class StaticPublishApplySummary
    {
        public int Logged { get; set; }

        public int Removed { get; set; }

        public int Failed { get; set; }

        public int KeptForRepublish { get; set; }
    }
}
