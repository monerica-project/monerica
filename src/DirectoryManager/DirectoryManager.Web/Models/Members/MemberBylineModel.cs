namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Input for the shared member byline partial (_MemberByline).</summary>
    public class MemberBylineModel
    {
        public MemberBylineModel(string? authorUserId, string? authorUsername)
        {
            this.AuthorUserId = authorUserId;
            this.AuthorUsername = authorUsername;
        }

        public string? AuthorUserId { get; }

        public string? AuthorUsername { get; }
    }
}
