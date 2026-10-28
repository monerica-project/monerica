namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// The kinds of things people submit to the site, as counted by the admin activity report.
    /// Values are stable (used for ordering and colors); add new kinds at the end.
    /// </summary>
    public enum SiteActivityType
    {
        NewListing = 1,
        ListingEdit = 2,
        Review = 3,
        ReviewReply = 4,
        ProblemReport = 5,
        VerificationRequest = 6,
    }
}
