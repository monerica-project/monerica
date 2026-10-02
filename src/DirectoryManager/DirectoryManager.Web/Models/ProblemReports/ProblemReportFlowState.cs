namespace DirectoryManager.Web.Models.ProblemReports
{
    public record ProblemReportFlowState
    {
        public int DirectoryEntryId { get; init; }

        public DateTime ExpiresUtc { get; init; }

        public bool CaptchaOk { get; set; }
    }
}
