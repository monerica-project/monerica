using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Members
{
    public sealed class KeyDetailViewModel
    {
        public KeyRow Key { get; set; } = new ();

        /// <summary>True when nothing was ever posted with this key (it can still be blocked).</summary>
        public bool HasNoPosts { get; set; }

        /// <summary>The block-list row (active or lifted), or null when never blocked.</summary>
        public BlockedReviewerKey? Block { get; set; }

        /// <summary>Posts hidden by the key block that unblocking can put back in the queue.</summary>
        public int HiddenByBlockCount { get; set; }

        public UserPostsPage Posts { get; set; } = new ();
    }
}
