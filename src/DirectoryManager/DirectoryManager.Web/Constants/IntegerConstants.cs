namespace DirectoryManager.Web.Constants
{
    public class IntegerConstants
    {
        public const int DefaultPageSize = 25;
        public const int MediumPageSize = 50;
        public const int MaxPageSize = 100;
        public const int CacheDurationSeconds = 86400; // 24 hours

        // Fixed slot-hold window (minutes): created when a buyer enters the checkout funnel and carried
        // via the reservation guid in the URL, so they can go back, change duration, and create invoices
        // within it. It is NOT extended by navigation or invoice creation — a hard cap so that abandoned
        // checkouts free the slot quickly (an unpaid invoice never blocks a slot beyond this window).
        public const int ReservationMinutes = 15;
        public const int NewestRevisionsToDisplay = 3;
        public const int DefaultAlternativePort = 8081;
        public const int DefaultRemoteHttpPort = 5055;
        public const int DefaultRemoteHttpsPort = 443;
        public const int DefaultDebuggingHttpPort = 5007;
        public const int DefaultDebuggingHttpsPort = 7145;
        public const int MinLengthCommentChars = 35;
        public const int MinLengthReplyChars = 10;
        public const int MaxAdditionalLinks = 3;
        public const int MaxGuarantees = 4;
        public const int ReviewsPageSize = 10;
        public const int ReviewCountToShowOnHomepage = 25;
        public const int CommentCountToShowOnHomepage = 10;
        public const int SessinExpiresMinutes = 20;
        public const int ChallengeLength = 10;
        public const int MaxVerifyAttempts = 10;
    }
}