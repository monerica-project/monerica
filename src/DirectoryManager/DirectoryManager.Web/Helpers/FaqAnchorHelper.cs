using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Maps a KYC policy or liquidity value to the id of the FAQ section that explains it
    /// (rendered on /faq from the FAQHtml content snippet), so listing pages can deep-link
    /// each value to its definition, e.g. /faq#kyc-rare or /faq#liquidity-third-party.
    /// Falls back to the umbrella section id if a value has no specific anchor.
    /// </summary>
    public static class FaqAnchorHelper
    {
        public static string KycPolicyAnchor(KycPolicy policy) => policy switch
        {
            KycPolicy.GuaranteedNoKyc => "kyc-guaranteed-no",
            KycPolicy.NotStated => "kyc-not-stated",
            KycPolicy.RareKyc => "kyc-rare",
            KycPolicy.ShotgunKyc => "kyc-shotgun",
            KycPolicy.MandatoryKyc => "kyc-mandatory",
            KycPolicy.VariesByProvider => "kyc-varies",
            _ => "kyc-policy",
        };

        public static string LiquidityAnchor(Liquidity liquidity) => liquidity switch
        {
            Liquidity.Own => "liquidity-own",
            Liquidity.Mixed => "liquidity-mixed",
            Liquidity.ThirdParty => "liquidity-third-party",
            Liquidity.VariesByProvider => "liquidity-varies",
            _ => "liquidity",
        };
    }
}
