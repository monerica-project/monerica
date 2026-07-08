namespace DirectoryManager.Common.Constants
{
    public class IntegerConstants
    {
        public const int MaxMainSponsoredListings = 5;

        public const int MinRequiredSubcategories = 2;
        public const int MaxSubcategorySponsoredListings = 1;

        public const int MinRequiredCategories = 2;
        public const int MaxCategorySponsoredListings = 1;

        public const int MaxMainSponsorsPerSubcategory = 5;

        /// <summary>A listing must be Verified AND listed in the directory at least this many
        /// days before it is eligible to become a sponsor.</summary>
        public const int MinimumDaysListedBeforeSponsoring = 180;
    }
}