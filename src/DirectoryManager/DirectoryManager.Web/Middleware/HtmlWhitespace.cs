using System.Text;
using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Middleware
{
    /// <summary>
    /// Pure string transform behind <see cref="HtmlWhitespaceTrimMiddleware"/>: removes
    /// whitespace-only lines and trailing spaces/tabs/CR, leaving the meaningful bytes of
    /// pre/textarea/script/style untouched. Only ever removes whitespace, so output renders
    /// identically. Kept separate from the middleware so it is trivially unit-testable.
    /// </summary>
    public static partial class HtmlWhitespace
    {
        // Blocks whose internal whitespace is significant — copied through byte-for-byte.
        [GeneratedRegex(
            @"<(pre|textarea|script|style)\b[^>]*>[\s\S]*?</\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ProtectedBlocks();

        public static string Collapse(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return html;
            }

            var sb = new StringBuilder(html.Length);
            var last = 0;

            foreach (Match m in ProtectedBlocks().Matches(html))
            {
                TrimSegment(html, last, m.Index, sb);          // squeeze the gap before this block
                sb.Append(html, m.Index, m.Length);            // ...but keep the block itself intact
                last = m.Index + m.Length;
            }

            TrimSegment(html, last, html.Length, sb);
            return sb.ToString();
        }

        // Drops whitespace-only lines and trailing spaces/tabs/CR from html[start..end).
        private static void TrimSegment(string s, int start, int end, StringBuilder sb)
        {
            var i = start;
            while (i < end)
            {
                var nl = s.IndexOf('\n', i);
                var hasNewline = nl >= 0 && nl < end;
                var lineEnd = hasNewline ? nl : end;

                var t = lineEnd;
                while (t > i && (s[t - 1] == ' ' || s[t - 1] == '\t' || s[t - 1] == '\r'))
                {
                    t--;
                }

                if (t > i)
                {
                    sb.Append(s, i, t - i);
                    if (hasNewline)
                    {
                        sb.Append('\n');
                    }
                }

                // A whitespace-only line contributes nothing (line + its newline are dropped).
                if (!hasNewline)
                {
                    break;
                }

                i = nl + 1;
            }
        }
    }
}
