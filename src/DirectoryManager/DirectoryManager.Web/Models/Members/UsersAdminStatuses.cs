namespace DirectoryManager.Web.Models.Members
{
    /// <summary>Account status filter values.</summary>
    public static class UsersAdminStatuses
    {
        public const string Current = "";       // everything except deleted (default)
        public const string Active = "active";
        public const string Blocked = "blocked"; // blocked from posting
        public const string Disabled = "disabled";
        public const string Deleted = "deleted";
        public const string All = "all";

        public static string Normalize(string? status) => status switch
        {
            Active or Blocked or Disabled or Deleted or All => status,
            _ => Current,
        };
    }
}
