using System.ComponentModel;

namespace DirectoryManager.Data.Enums
{
    /// <summary>
    /// Whether the listing's website needs JavaScript to work. Replaces the old
    /// "No JavaScript" / "JavaScript Optional" tags. <see cref="Unknown"/> (0) is the default and
    /// is shown in forms as "Not Stated"; it renders nothing on the listing page.
    /// </summary>
    public enum SiteJavaScript
    {
        [Description("Not Stated")]
        Unknown = 0,

        [Description("Required")]
        Required = 1,

        [Description("Optional")]
        Optional = 2,

        /// <summary>Retired: merged into <see cref="Optional"/> (2026-11-08). Kept so old rows/URLs still parse; shown as "Optional".</summary>
        [Description("Optional")]
        NotRequired = 3,

        /// <summary>The website uses no JavaScript at all.</summary>
        [Description("None")]
        None = 4,
    }

    public static class SiteJavaScriptExtensions
    {
        /// <summary>Dropdown order: "Not Stated" (the default) first, then the three real answers.</summary>
        public static readonly IReadOnlyList<SiteJavaScript> FormOrder = new[]
        {
            SiteJavaScript.Unknown,
            SiteJavaScript.Required,
            SiteJavaScript.Optional,
            SiteJavaScript.None,
        };

        /// <summary>
        /// Filter checkboxes, like the KYC filter: every box is ticked by default (= no filter) and
        /// unticking one hides those listings. "Not Stated" is included so all-ticked shows everything.
        /// </summary>
        public static readonly IReadOnlyList<SiteJavaScript> Filterable = new[]
        {
            SiteJavaScript.None,
            SiteJavaScript.Optional,
            SiteJavaScript.Required,
            SiteJavaScript.Unknown,
        };

        /// <summary>Plain-English meaning of each value (profile, forms and filter use this one text).</summary>
        /// <returns>The explanation, or empty for Not Stated.</returns>
        public static string Explanation(this SiteJavaScript value) => value switch
        {
            SiteJavaScript.None => "The website uses no JavaScript at all.",
            SiteJavaScript.Optional or SiteJavaScript.NotRequired => "The main features work with JavaScript turned off; some extras may need it.",
            SiteJavaScript.Required => "The website does not work without JavaScript.",
            _ => string.Empty,
        };

        /// <summary>True when the value should be shown publicly (anything but Not Stated).</summary>
        /// <returns>Whether to show it.</returns>
        public static bool IsShown(this SiteJavaScript value) => value != SiteJavaScript.Unknown;

        /// <summary>Old "Not Required" values count as Optional.</summary>
        /// <returns>The value with retired options mapped to their replacement.</returns>
        public static SiteJavaScript Normalize(this SiteJavaScript value) => value == SiteJavaScript.NotRequired ? SiteJavaScript.Optional : value;
    }
}
