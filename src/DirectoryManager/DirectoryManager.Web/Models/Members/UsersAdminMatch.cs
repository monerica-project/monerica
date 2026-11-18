namespace DirectoryManager.Web.Models.Members
{
    /// <summary>How a fingerprint search matches the normalized hex.</summary>
    public static class UsersAdminMatch
    {
        public const string Contains = "contains";
        public const string Prefix = "prefix";
        public const string Suffix = "suffix";

        public static string Normalize(string? match) => match switch
        {
            Prefix => Prefix,
            Suffix => Suffix,
            _ => Contains,
        };
    }
}
