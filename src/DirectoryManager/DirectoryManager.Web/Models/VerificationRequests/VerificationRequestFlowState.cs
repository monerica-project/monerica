namespace DirectoryManager.Web.Models.VerificationRequests
{
    public record VerificationRequestFlowState
    {
        public int DirectoryEntryId { get; init; }

        public DateTime ExpiresUtc { get; init; }

        public bool CaptchaOk { get; set; }
    }
}
