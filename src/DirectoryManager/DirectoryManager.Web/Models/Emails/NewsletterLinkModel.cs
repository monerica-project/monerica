namespace DirectoryManager.Web.Models.Emails
{
    /// <summary>
    /// A signed link from an email (newsletter confirmation or one-click unsubscribe) whose token
    /// checked out. Pages get null when the link is invalid or expired.
    /// </summary>
    public class NewsletterLinkModel
    {
        public string Email { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the action (unsubscribe) has been done.</summary>
        public bool Done { get; set; }
    }
}
