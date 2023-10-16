using System.ComponentModel;

namespace DirectoryManager.Web.Enums
{
    public enum SponsoredListingOffers
    {
        None = 0,

        /// <summary>
        /// A promotional offer allowing your listing to be sponsored for 7 days. Ideal for short-term promotions or to increase visibility for a week.
        /// </summary>
        [Description("7-Day Sponsored Listing")]
        SevenDays = 1,

        /// <summary>
        /// A value offer to keep your listing sponsored for an entire month. Suitable for seasonal promotions or monthly campaigns.
        /// </summary>
        [Description("30-Day Sponsored Listing")]
        ThirtyDays = 2,

        /// <summary>
        /// The ultimate sponsored listing package offering 3 months of increased visibility. Perfect for major campaigns or to establish a strong presence.
        /// </summary>
        [Description("90-Day Sponsored Listing")]
        NinetyDays = 3,
    }
}