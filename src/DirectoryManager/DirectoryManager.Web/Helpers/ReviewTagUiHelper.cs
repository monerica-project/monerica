using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    public static class ReviewTagUiHelper
    {
        // Bootstrap 5 badge background classes (or swap to your own CSS)
        public static string BadgeClass(ReviewTagLevel level) => level switch
        {
            ReviewTagLevel.Verified => "bg-success",
            ReviewTagLevel.Info => "bg-info",
            ReviewTagLevel.Warning => "bg-warning text-dark",
            ReviewTagLevel.Risk => "bg-orange text-dark",     // if you have custom orange
            ReviewTagLevel.KycIssue => "bg-danger",
            ReviewTagLevel.Scam => "bg-dark",
            _ => "bg-secondary"
        };
    }
}
