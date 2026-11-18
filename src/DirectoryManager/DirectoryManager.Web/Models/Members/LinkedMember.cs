namespace DirectoryManager.Web.Models.Members
{
    public sealed class LinkedMember
    {
        public string Id { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public bool IsPostingBlocked { get; set; }
    }
}
