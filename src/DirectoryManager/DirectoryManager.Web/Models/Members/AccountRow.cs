namespace DirectoryManager.Web.Models.Members
{
    public sealed class AccountRow
    {
        public string Id { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public DateTime CreateDate { get; set; }

        public bool IsDeleted { get; set; }

        public bool IsDisabled { get; set; }

        public bool IsPostingBlocked { get; set; }

        public bool IsLockedOut { get; set; }

        public string? PgpFingerprint { get; set; }

        public bool LinkedKeyBlocked { get; set; }

        public PostStatusCounts Reviews { get; set; } = new ();

        public PostStatusCounts Replies { get; set; } = new ();

        public DateTime? LastPostUtc { get; set; }

        public string StatusLabel =>
            this.IsDeleted ? "Deleted"
            : this.IsDisabled ? "Disabled"
            : this.IsPostingBlocked ? "Blocked from posting"
            : "Active";
    }
}
