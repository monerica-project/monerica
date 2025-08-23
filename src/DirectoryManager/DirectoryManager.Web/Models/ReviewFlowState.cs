namespace DirectoryManager.Web.Models
{
    public record ReviewFlowState
    {
        public int DirectoryEntryId { get; init; }
        public DateTime ExpiresUtc { get; init; }
        public bool CaptchaOk { get; set; }
        public string? PgpArmored { get; set; }
        public string? PgpFingerprint { get; set; }
        public int? ChallengeCode { get; set; }
        public string? ChallengeCiphertext { get; set; }
        public bool ChallengeSolved { get; set; }
    }
}
