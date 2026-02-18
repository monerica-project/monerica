namespace DirectoryManager.Web.Models
{
    public class ReviewReplyFlowState
    {
        public int DirectoryEntryReviewId { get; set; }
        public int DirectoryEntryId { get; set; }

        public bool CaptchaOk { get; set; }

        public string? PgpArmored { get; set; }
        public string? PgpFingerprint { get; set; }

        public string? ChallengeCode { get; set; }

        public string? ChallengeCiphertext { get; set; }
        public bool ChallengeSolved { get; set; }

        public int VerifyAttempts { get; set; }

        public DateTime ExpiresUtc { get; set; }
    }
}