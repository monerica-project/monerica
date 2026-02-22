namespace DirectoryManager.Web.Models
{
    public sealed class UserContentModerationResult
    {
        public bool IsValid { get; init; }
        public bool NeedsManualReview { get; init; }

        // If IsValid == false, show this to the user (ModelState error)
        public string? ValidationErrorMessage { get; init; }

        // What to show on the Thank You page
        public string ThankYouMessage { get; init; } = string.Empty;
    }
}
