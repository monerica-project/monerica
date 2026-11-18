namespace DirectoryManager.Web.Models.Members
{
    public sealed class AccountDetailViewModel
    {
        public AccountRow Account { get; set; } = new ();

        public DateTime? PgpLinkedUtc { get; set; }

        public DateTime? DisabledUtc { get; set; }

        public DateTime? PostingBlockedUtc { get; set; }

        public string? PostingBlockedBy { get; set; }

        public string? PostingBlockReason { get; set; }

        public DateTime? PostingUnblockedUtc { get; set; }

        public string? PostingUnblockedBy { get; set; }

        /// <summary>Posts hidden by an account posting block that unblocking can put back in the queue.</summary>
        public int HiddenByBlockCount { get; set; }

        public UserPostsPage Posts { get; set; } = new ();
    }
}
