using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Finds a business's own PGP public key: an armored key block pasted in a page, or links
    /// to one (".asc" files, "PGP"/"GPG" links, keys.openpgp.org). Pure functions; the reviewer
    /// fetches the pages. Only the armor shape is checked here; the admin page shows the
    /// fingerprint (parsed with BouncyCastle) before anything is approved.
    /// </summary>
    public static class PgpKeyScan
    {
        public const string BeginMarker = "-----BEGIN PGP PUBLIC KEY BLOCK-----";
        public const string EndMarker = "-----END PGP PUBLIC KEY BLOCK-----";

        /// <summary>Real keys are a few hundred bytes to a few KB; anything far outside that is not one.</summary>
        public const int MinKeyLength = 200;
        public const int MaxKeyLength = 64_000;

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private static readonly Regex HrefRx = new Regex(
            @"<a\b[^>]*?\bhref\s*=\s*[""'](?<href>[^""'#]+)[""'][^>]*>(?<text>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, Timeout);

        private static readonly Regex TagRx = new Regex("<[^>]+>", RegexOptions.CultureInvariant, Timeout);

        private static readonly Regex KeyWordRx = new Regex(
            @"\b(pgp|gpg|openpgp|public[\s_-]?key)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        private static readonly Regex Base64LineRx = new Regex(
            @"^[A-Za-z0-9+/=]+$", RegexOptions.CultureInvariant, Timeout);

        /// <summary>
        /// The first armored public key block in <paramref name="content"/> (HTML or plain text),
        /// cleaned to plain armor with \n line breaks, or null when there is no well-formed one.
        /// </summary>
        public static string? ExtractArmoredKey(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            var start = content.IndexOf(BeginMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            var end = content.IndexOf(EndMarker, start, StringComparison.Ordinal);
            if (end < 0)
            {
                return null;
            }

            var raw = content.Substring(start, end + EndMarker.Length - start);

            // A key shown in HTML: <br> line breaks, tags, entities.
            raw = Regex.Replace(raw, @"<br\s*/?>[ \t]*(\r?\n)?", "\n", RegexOptions.IgnoreCase, Timeout);
            raw = TagRx.Replace(raw, string.Empty);
            raw = WebUtility.HtmlDecode(raw).Replace("\r\n", "\n").Replace('\r', '\n');

            var lines = raw.Split('\n').Select(l => l.Trim()).ToList();
            if (lines.Count < 3 || lines[0] != BeginMarker || lines[^1] != EndMarker)
            {
                return null;
            }

            // Armor headers ("Comment: ...", "Version: ...") sit before the first blank line.
            var body = new List<string>();
            var headers = new List<string>();
            foreach (var line in lines.Skip(1).Take(lines.Count - 2))
            {
                // Headers can only come before the first base64 line.
                if (body.Count == 0 && line.Contains(':', StringComparison.Ordinal) && !Base64LineRx.IsMatch(line))
                {
                    headers.Add(line);
                    continue;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                // Checksum line "=abcd" is allowed; everything else must be base64.
                if (!Base64LineRx.IsMatch(line))
                {
                    return null;
                }

                body.Add(line);
            }

            if (body.Count == 0)
            {
                return null;
            }

            var key = BeginMarker + "\n" +
                (headers.Count > 0 ? string.Join("\n", headers) + "\n" : string.Empty) +
                "\n" + string.Join("\n", body) + "\n" + EndMarker + "\n";

            return key.Length is < MinKeyLength or > MaxKeyLength ? null : key;
        }

        /// <summary>
        /// Links on the page that probably lead to the site's PGP key: same site (or a subdomain),
        /// or keys.openpgp.org. Ordered best first (.asc files before "PGP" pages), at most 3.
        /// </summary>
        public static IReadOnlyList<string> KeyLinks(string? html, Uri? pageUri)
        {
            if (string.IsNullOrEmpty(html) || pageUri == null)
            {
                return Array.Empty<string>();
            }

            MatchCollection matches;
            try
            {
                matches = HrefRx.Matches(html);
            }
            catch (RegexMatchTimeoutException)
            {
                return Array.Empty<string>();
            }

            var found = new List<(string Url, int Rank)>();
            foreach (Match m in matches)
            {
                var href = WebUtility.HtmlDecode(m.Groups["href"].Value.Trim());
                if (!Uri.TryCreate(pageUri, href, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    continue;
                }

                var path = uri.AbsolutePath;
                var text = WebUtility.HtmlDecode(TagRx.Replace(m.Groups["text"].Value, " "));
                int rank;
                if (path.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".gpg", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".pgp", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
                {
                    rank = 0;
                }
                else if (uri.Host.Equals("keys.openpgp.org", StringComparison.OrdinalIgnoreCase) &&
                         path.StartsWith("/vks/", StringComparison.OrdinalIgnoreCase))
                {
                    rank = 1;
                }
                else if (KeyWordRx.IsMatch(path.Replace('/', ' ').Replace('-', ' ').Replace('_', ' ')) || KeyWordRx.IsMatch(text))
                {
                    rank = 2;
                }
                else
                {
                    continue;
                }

                var ownSite = SameSite(uri, pageUri);
                if (!ownSite && rank != 1)
                {
                    continue;
                }

                var url = uri.GetLeftPart(UriPartial.Query);
                if (!found.Any(f => string.Equals(f.Url, url, StringComparison.OrdinalIgnoreCase)))
                {
                    found.Add((url, rank));
                }
            }

            return found.OrderBy(f => f.Rank).Select(f => f.Url).Take(3).ToList();
        }

        /// <summary>Same registrable-ish site: same host, or one is a subdomain of the other (www, docs, ...).</summary>
        private static bool SameSite(Uri a, Uri b)
        {
            var ha = a.Host.ToLowerInvariant();
            var hb = b.Host.ToLowerInvariant();
            return ha == hb || ha.EndsWith("." + hb, StringComparison.Ordinal) || hb.EndsWith("." + ha, StringComparison.Ordinal)
                || (ha.StartsWith("www.", StringComparison.Ordinal) && ha[4..] == hb)
                || (hb.StartsWith("www.", StringComparison.Ordinal) && hb[4..] == ha);
        }
    }
}
