namespace DirectoryManager.Web.Models.Reviews
{
    public class ReviewReplyFlowState
    {
        /// <summary>Guards key submission and code verification. Once a challenge is solved the
        /// fingerprint is frozen, so a racing submit-key can't swap in someone else's key.</summary>
        public object Sync { get; } = new object();

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