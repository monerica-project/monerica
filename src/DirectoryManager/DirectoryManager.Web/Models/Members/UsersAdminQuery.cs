namespace DirectoryManager.Web.Models.Members
{
    public sealed class UsersAdminQuery
    {
        public string Tab { get; set; } = UsersAdminTabs.Accounts;

        /// <summary>Username contains (accounts tab).</summary>
        public string? Q { get; set; }

        /// <summary>Fingerprint / key id (hex; spaces and 0x ignored).</summary>
        public string? Key { get; set; }

        public string Match { get; set; } = UsersAdminMatch.Contains;

        public string Status { get; set; } = UsersAdminStatuses.Current;

        public string Sort { get; set; } = UsersAdminSorts.Newest;

        public int Page { get; set; } = 1;
    }
}
