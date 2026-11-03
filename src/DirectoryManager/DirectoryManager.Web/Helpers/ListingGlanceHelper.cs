using System.Globalization;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Small, testable pieces of the listing page's "At a glance" panel: the fee range a listing
    /// states in its note or description, and how long a service has been operating.
    /// </summary>
    public static class ListingGlanceHelper
    {
        // "1-2% fee", "0.5% fee", "fee: 0.4–0.8%", "fees 1%"; a range uses - or – between numbers.
        private static readonly Regex FeeBeforeWord = new (
            @"(?<!\d)(\d+(?:\.\d+)?)\s*%?\s*(?:[-–]|to)\s*(\d+(?:\.\d+)?)\s*%\s*(?:swap\s+)?fees?\b|(?<![\d.])(\d+(?:\.\d+)?)\s*%\s*(?:swap\s+)?fees?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

        private static readonly Regex FeeAfterWord = new (
            @"\bfees?\s*[:=]?\s*(?:of\s+|from\s+)?(\d+(?:\.\d+)?)\s*%?\s*(?:(?:[-–]|to)\s*(\d+(?:\.\d+)?)\s*)?%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

        /// <summary>
        /// The fee a listing states in its own words, e.g. "1–2%" or "0.5%"; null when none is stated.
        /// The note is checked first (that's where fees are usually written), then the description.
        /// </summary>
        public static string? StatedFee(string? note, string? description)
        {
            foreach (var text in new[] { note, description })
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var m = FeeBeforeWord.Match(text);
                if (m.Success)
                {
                    return m.Groups[3].Success
                        ? $"{m.Groups[3].Value}%"
                        : $"{m.Groups[1].Value}–{m.Groups[2].Value}%";
                }

                m = FeeAfterWord.Match(text);
                if (m.Success)
                {
                    return m.Groups[2].Success
                        ? $"{m.Groups[1].Value}–{m.Groups[2].Value}%"
                        : $"{m.Groups[1].Value}%";
                }
            }

            return null;
        }

        /// <summary>
        /// "Aug 2025 (1 yr)", "Mar 2026 (8 mo)", "Oct 2026 (new)": when a service started and how long ago.
        /// </summary>
        public static string? OperatingSince(DateOnly? founded, DateTime nowUtc)
        {
            if (founded is not { } f)
            {
                return null;
            }

            var start = f.ToDateTime(TimeOnly.MinValue);
            if (start > nowUtc)
            {
                return null;
            }

            var months = ((nowUtc.Year - start.Year) * 12) + nowUtc.Month - start.Month - (nowUtc.Day < start.Day ? 1 : 0);
            var age = months >= 12
                ? $"{months / 12} yr{(months / 12 == 1 ? string.Empty : "s")}"
                : months >= 1 ? $"{months} mo" : "new";

            return $"{start.ToString("MMM yyyy", CultureInfo.InvariantCulture)} ({age})";
        }
    }
}
