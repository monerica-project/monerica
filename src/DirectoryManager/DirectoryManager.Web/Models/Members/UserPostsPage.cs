namespace DirectoryManager.Web.Models.Members
{
    public sealed class UserPostsPage
    {
        public List<UserPostRow> Posts { get; set; } = new ();

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int Total { get; set; }
    }
}
