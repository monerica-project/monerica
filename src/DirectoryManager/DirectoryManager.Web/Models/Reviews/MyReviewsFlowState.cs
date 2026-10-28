using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Reviews
{
    public class MyReviewsFlowState
    {
        /// <summary>Guards key submission and code verification. Once a challenge is solved the
        /// fingerprint is frozen, so a racing submit-key can't swap in someone else's key.</summary>
        public object Sync { get; } = new object();

        public DateTime ExpiresUtc { get; set; }

        public bool CaptchaOk { get; set; }

        public string? PgpArmored { get; set; }
        public string? PgpFingerprint { get; set; }

        public string? ChallengeCode { get; set; } // normalized
        public string? ChallengeCiphertext { get; set; } // armored cipher text
        public int VerifyAttempts { get; set; }
        public bool ChallengeSolved { get; set; }
    }
}
