namespace DirectoryManager.Data.Enums
{
    public enum SiteConfigSetting
    {
        Unknown = 0,
        HeaderHtml = 1,
        FooterHtml = 2,
        HomePageDisplayHtml = 3,
        PaymentCurrencyMessage = 4,
        HomePageMetaTags = 5,
        DirectoryLegendHtml = 7,
        HomePageTitle = 8,
        DonationHtml = 9,
        NowPaymentsMessageHtml = 10,
        SubmissionGuidelinesHtml = 11,
        CdnPrefixWithProtocol = 13,
        BlobPrefix = 14,
        Link2Name = 15,
        Link3Name = 16,
        ContactHtml = 17,
        SuccessPageHtml = 18,
        MainSponsoredListingDetails = 19,
        SiteName = 20,
        SubcategorySponsoredListingDetails = 21,
        TopBannerHtml = 22,
        FullRootUrl = 23,
        CanonicalDomain = 24,
        FullRootTorUrl = 25,
        TopMenuActionsHtml = 26,
        InvoiceSuccessMessageHtml = 27,
        EmailSettingUnsubscribeEmail = 28,
        EmailSettingUnsubscribeUrlFormat = 29,
        EmailSettingUnsubscribeFooterText = 30,
        EmailSettingUnsubscribeFooterHtml = 31,
        NewsletterSummaryHtml = 35,
        SiteLogoUrl = 36,
        CategorySponsoredListingDetails = 37,
        SponsoredListingSteps = 38,
        AppDomain = 39,
        TextIntro = 40,
        FAQHtml = 41,
        SearchBlacklistRedirectUrl = 42,
        AboutHtml = 43,
        PgpKey = 44,
        PgpInstructionsHtml = 45,
        HtmlNotificationSnippet = 46,
        TermsAndConditionsHtml = 47,

        // "true" switches the public video-sponsorship pages and checkout on.
        VideoSponsorshipEnabled = 48,

        // The YouTube channel the sponsored videos are published on.
        VideoSponsorshipChannelUrl = 49,

        // Extra Main Sponsor benefit bullets (raw HTML <li> items) on /sponsorship/main — e.g. the
        // partner/network sites and social/blog perks. Empty = those bullets are not shown.
        MainSponsorExtraBenefitsHtml = 50,

        // Absolute URL of the logo shown in the copy-and-paste "Review us" badge on listing
        // pages (must work on external sites, so a full CDN URL). Empty = badge has no logo.
        ReviewBadgeLogoUrl = 51,

        // What the directory's listings accept/relate to, e.g. "Monero (XMR)". Used in llms.txt;
        // the part before " (" is the short form ("Monero"). Empty = generic wording.
        DirectoryCurrencyName = 52
    }
}