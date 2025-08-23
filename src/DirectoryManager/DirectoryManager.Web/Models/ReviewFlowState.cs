namespace DirectoryManager.Web.Models
{
    public record ReviewFlowState
    {
        public int DirectoryEntryId { get; init; }
        public DateTime ExpiresUtc { get; init; }
        public bool CaptchaOk { get; set; }
        public string? PgpArmored { get; set; }
        public string? PgpFingerprint { get; set; }
        public int? ChallengeCode { get; set; }          // 6 digits (100000–999999)
        public string? ChallengeCiphertext { get; set; } // Encrypted-to-key blob
        public bool ChallengeSolved { get; set; }
    }
}
