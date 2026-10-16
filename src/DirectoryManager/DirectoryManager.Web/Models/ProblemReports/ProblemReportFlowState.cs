namespace DirectoryManager.Web.Models.ProblemReports
{
    public record ProblemReportFlowState
    {
        public int DirectoryEntryId { get; init; }

        public DateTime ExpiresUtc { get; init; }

        public bool CaptchaOk { get; set; }

        // Set once the message is saved, so a repeated submit (double-click, back + resubmit)
        // lands on the same pay page instead of losing the flow or saving a duplicate.
        public Guid? SavedPaymentToken { get; set; }

        // Serializes submits for this one flow, so two simultaneous posts can't both save.
        public SemaphoreSlim SaveGate { get; } = new (1, 1);
    }
}
