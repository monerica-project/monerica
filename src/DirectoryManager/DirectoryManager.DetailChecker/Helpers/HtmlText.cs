using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.DetailChecker.Helpers
{
    /// <summary>
    /// Cheap HTML → readable-text reduction. Good enough to keyword-scan and to feed a
    /// small local model — we do not need a full DOM. No external dependency on purpose
    /// (this is a lightweight batch job).
    /// </summary>
    public static class HtmlText
    {
        private static readonly Regex ScriptStyle = new(
            "<(script|style|noscript|svg)[^>]*>.*?</\\1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex Whitespace = new("[ \\t\\f\\v]+", RegexOptions.Compiled);
        private static readonly Regex BlankLines = new("\\n{3,}", RegexOptions.Compiled);

        public static string ToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var s = ScriptStyle.Replace(html, " ");

            // Turn common block boundaries into newlines so paragraphs stay separated.
            s = Regex.Replace(s, "<(br|/p|/div|/li|/h[1-6]|/tr)[^>]*>", "\n", RegexOptions.IgnoreCase);

            s = Tags.Replace(s, " ");
            s = WebUtility.HtmlDecode(s);
            s = Whitespace.Replace(s, " ");
            s = BlankLines.Replace(s, "\n\n");
            return s.Trim();
        }
    }
}
