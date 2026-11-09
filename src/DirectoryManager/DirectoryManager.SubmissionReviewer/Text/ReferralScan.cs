using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Finds signs of a referral / affiliate program that a plain (no-JavaScript) fetch can miss.
    /// Many sites build their menu and footer in JavaScript, so the links only exist in the
    /// site's own script files. Pure functions; the reviewer fetches the scripts.
    /// </summary>
    public static class ReferralScan
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        // Path segment words that mean a referral/affiliate program page.
        private const string Words = @"referrals?|refer-a-friend|refer|affiliates?|affiliate-program|affiliate-programme|partner-program|partners-program|invite-friends";

        // A quoted app route such as "/referrals" or '/affiliate/dashboard' inside a script.
        private static readonly Regex RouteRx = new Regex(
            "[\"'`](?<path>/(?:" + Words + @")(?:/[\w\-]*)?)[""'`]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        // A same-origin script file: <script src="/assets/index-abc.js"> or <link rel="modulepreload" href="...">.
        private static readonly Regex ScriptSrcRx = new Regex(
            @"<(?:script\b[^>]*\bsrc|link\b[^>]*\brel=[""']?modulepreload[""']?[^>]*\bhref)\s*=\s*[""'](?<src>[^""']+\.m?js(?:\?[^""']*)?)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        private static readonly Regex HrefWordRx = new Regex(
            @"(?:^|[/\-_.?=&])(?:" + Words + @")(?:$|[/\-_.?=&#])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        private static readonly Regex TextWordRx = new Regex(
            @"\b(?:referral|referrals|refer a friend|affiliate|affiliates|affiliate program|partner program|invite (?:and|&) earn|earn with us)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        /// <summary>Same-site script URLs on the page (at most <paramref name="max"/>, in page order).</summary>
        public static IReadOnlyList<string> ScriptUrls(string? html, Uri pageUri, int max = 3)
        {
            var list = new List<string>();
            foreach (Match m in ScriptSrcRx.Matches(html ?? string.Empty))
            {
                if (!Uri.TryCreate(pageUri, WebUtility.HtmlDecode(m.Groups["src"].Value.Trim()), out var abs) ||
                    (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) ||
                    !string.Equals(abs.Host, pageUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = abs.ToString();
                if (!list.Contains(url, StringComparer.Ordinal))
                {
                    list.Add(url);
                }

                if (list.Count >= max)
                {
                    break;
                }
            }

            return list;
        }

        /// <summary>Referral/affiliate routes named in a script, e.g. "/referrals" (distinct, lower-case).</summary>
        public static IReadOnlyList<string> RoutesInScript(string? script) =>
            RouteRx.Matches(script ?? string.Empty)
                .Select(m => m.Groups["path"].Value.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();

        /// <summary>Links in the HTML whose URL or text points to a referral/affiliate program.</summary>
        public static IReadOnlyList<string> LinksInHtml(string? html, Uri pageUri) =>
            HtmlText.Links(html, pageUri)
                .Where(l => (Uri.TryCreate(l.Url, UriKind.Absolute, out var u) && HrefWordRx.IsMatch(u.AbsolutePath + u.Query))
                            || TextWordRx.IsMatch(l.Text ?? string.Empty))
                .Select(l => string.IsNullOrWhiteSpace(l.Text) ? l.Url : $"{l.Text} ({l.Url})")
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();
    }
}
