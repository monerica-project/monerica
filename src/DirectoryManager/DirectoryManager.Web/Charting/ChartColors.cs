using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Charting
{
    /// <summary>
    /// Dark-theme chart colours shared by the chart renderer and the page text that describes
    /// the charts, so the words ("the yellow dashed line") always match what is drawn.
    /// </summary>
    public static class ChartColors
    {
        public const string DarkBackgroundHex = "#05070a";
        public const string DarkInkHex = "#f2f2f2";      // overlay lines (your subcategory) + labels
        public const string DarkMutedHex = "#9aa3ad";
        public const string DarkGridHex = "#1f242b";
        public const string DarkNowHex = "#ffe14d";      // bright yellow: today's price
        public const string PurchaseNeonHex = "#ff4dd2"; // hot pink: new purchases (dark theme)
        public const string PurchaseHex = "#c2185b";     // new purchases (light theme)

        public static string TypeNeonHex(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "#ff8c1a",        // neon orange
            SponsorshipType.CategorySponsor => "#39ff7a",    // neon green
            SponsorshipType.SubcategorySponsor => "#22d3ff", // neon cyan
            _ => "#c8c8c8",
        };

        public static string TypeNeonName(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "orange",
            SponsorshipType.CategorySponsor => "green",
            SponsorshipType.SubcategorySponsor => "blue",
            _ => "grey",
        };
    }
}
