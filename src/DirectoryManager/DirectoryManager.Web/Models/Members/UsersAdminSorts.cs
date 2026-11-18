namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Sort orders shared by both lists.</summary>
    public static class UsersAdminSorts
    {
        public const string Newest = "newest";
        public const string MostPosts = "posts";
        public const string LastActive = "active";

        public static string Normalize(string? sort) => sort switch
        {
            MostPosts => MostPosts,
            LastActive => LastActive,
            _ => Newest,
        };
    }
}
