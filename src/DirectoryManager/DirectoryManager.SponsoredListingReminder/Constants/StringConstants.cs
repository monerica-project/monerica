namespace DirectoryManager.SponsoredListingReminder.Constants
{
    public class StringConstants
    {
        public const string ListingNameToken = "[LISTING_NAME]";
        public const string ListingRenewalLinkToken = "[LISTING_RENEWAL_LINK]";
        public const string DirectoryEntryIdPlaceholder = "[DIRECTORY_ENTRY_ID]";
        public const string SponsorshipTypePlaceholder = "[SPONSORSHIP_TYPE]";
        public const string SubCategoryIdPlaceholder = "[SUBCATEGORY_ID]";
        public const string SponsoredListingExpirationReminder = "sponsored-listing-expiration-reminder";
        public const string DefaultListingName = "Your Sponsored Listing";

        // Final (24 hours or less) reminder: template key and the extra tokens both templates may use.
        public const string SponsoredListingFinalReminder = "sponsored-listing-final-reminder";
        public const string ExpiresAtToken = "[EXPIRES_AT]";
        public const string HoursLeftToken = "[HOURS_LEFT]";
        public const string PlacementToken = "[PLACEMENT]";
        public const string WaitlistLineToken = "[WAITLIST_LINE]";
    }
}