namespace DirectoryManager.Web.Models.Members
{
    public class MemberAccountViewModel
    {
        public string Username { get; set; } = string.Empty;

        public DateTime MemberSinceUtc { get; set; }

        public int ReviewCount { get; set; }

        public int ReplyCount { get; set; }

        /// <summary>Fingerprint of the linked (proven) PGP key, or null.</summary>
        public string? PgpFingerprint { get; set; }
    }
}
