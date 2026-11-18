namespace DirectoryManager.Web.Models.Members
{
    public class MemberReplyPageViewModel
    {
        public int DirectoryEntryReviewId { get; set; }

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public string DirectoryEntryName { get; set; } = string.Empty;

        public string ReviewExcerpt { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public MemberReplyInputModel Input { get; set; } = new ();
    }
}
