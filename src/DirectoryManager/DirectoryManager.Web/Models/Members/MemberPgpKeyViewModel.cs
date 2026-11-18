namespace DirectoryManager.Web.Models.Members
{
    /// <summary>The member's PGP key page: the linked key (if any) and the add / replace / remove forms.</summary>
    public class MemberPgpKeyViewModel
    {
        public string Username { get; set; } = string.Empty;

        /// <summary>Fingerprint of the linked key, or null when none is linked.</summary>
        public string? LinkedFingerprint { get; set; }

        public DateTime? LinkedUtc { get; set; }

        /// <summary>Set while a code was sent for a key and not confirmed yet.</summary>
        public string? PendingFingerprint { get; set; }

        /// <summary>Re-filled into the textarea after an error (never stored).</summary>
        public string? PgpArmored { get; set; }

        public bool HasKey => !string.IsNullOrEmpty(this.LinkedFingerprint);
    }
}
