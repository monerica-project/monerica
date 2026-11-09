using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Short hover explanations for the filter page's Status, KYC policy and Liquidity options.
    /// Shortened from the FAQ definitions (FAQHtml snippet) so the two stay in step; shown as a
    /// plain title attribute, so they work without JavaScript.
    /// </summary>
    public static class FilterOptionTooltips
    {
        public static string For(DirectoryStatus status) => status switch
        {
            DirectoryStatus.Admitted => "Added to the directory but not yet tested by the community.",
            DirectoryStatus.Verified => "Tested by at least one member of the community.",
            DirectoryStatus.Questionable => "Doubts about its claims or reliability, but no confirmed reports of theft.",
            DirectoryStatus.Scam => "Credible evidence it has taken users' funds without delivering.",
            _ => string.Empty,
        };

        public static string For(KycPolicy policy) => policy switch
        {
            KycPolicy.GuaranteedNoKyc => "Will never ask for identity verification. If it can't process your swap, it refunds you instead of asking for documents.",
            KycPolicy.NotStated => "The provider hasn't published a KYC policy, so its stance is unknown.",
            KycPolicy.RareKyc => "KYC is uncommon but can happen: funds can be frozen after manual reports, and you may need to prove where they came from.",
            KycPolicy.ShotgunKyc => "Automated screening checks every deposit. Flagged swaps are paused, and you may have to verify your identity to get a refund.",
            KycPolicy.MandatoryKyc => "Identity verification is always required before you can use the service.",
            KycPolicy.VariesByProvider => "Routes orders to several providers, each with its own KYC policy. It depends on which one fills your order.",
            _ => string.Empty,
        };

        public static string For(SiteJavaScript js) => js switch
        {
            SiteJavaScript.Unknown => "The listing has not said whether its website needs JavaScript.",
            _ => js.Explanation(),
        };

        public static string For(Liquidity liquidity) => liquidity switch
        {
            Liquidity.Own => "The provider fills every swap with its own funds, under its own control.",
            Liquidity.Mixed => "Part of the liquidity is the provider's own and part comes from third parties.",
            Liquidity.OwnAndThirdParty => "Different swap modes use different sources: some are filled from its own funds, others by a third party.",
            Liquidity.ThirdParty => "Swaps are filled by other exchanges or parties, with this provider as a front end. Your funds may be inspected by them.",
            Liquidity.VariesByProvider => "Aggregates several providers, so where the liquidity comes from depends on which one fills your order.",
            _ => string.Empty,
        };
    }
}
