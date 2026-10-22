using System.Text.RegularExpressions;

namespace DirectoryManager.Web.Helpers
{
    public class UrlHelper
    {
        // %XX escapes for ASCII characters that carry URL meaning (or are % / \\ themselves).
        private static readonly Regex EscapedStructureRegex = new (
            @"%(?:2[0-9A-Fa-f]|3[A-Fa-f]|40|5[BbCcDd]|7[CcEe])",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(250));

        /// <summary>
        /// Human-readable form of a URL for link TEXT: percent-escapes are decoded so
        /// "%D0%BA%D1%80" shows as "кр". The href itself should keep the original (encoded)
        /// URL. Returns the input unchanged if it isn't valid escaped UTF-8 or if decoding would
        /// reveal control, invisible or text-direction characters (which can disguise a link).
        /// </summary>
        public static string ToDisplayUrl(string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.Contains('%'))
            {
                return url ?? string.Empty;
            }

            // Never decode an escape that hides URL structure (/ ? # @ : etc.): decoded text like
            // "paypal.com%2Flogin@evil.com" would DISPLAY one host while linking to another.
            if (EscapedStructureRegex.IsMatch(url))
            {
                return url;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(url);
            }
            catch (UriFormatException)
            {
                return url;
            }

            foreach (var ch in decoded)
            {
                var cat = char.GetUnicodeCategory(ch);
                if (char.IsControl(ch)
                    || char.IsWhiteSpace(ch)
                    || ch == '\uFFFD' // invalid UTF-8 became a replacement char
                    || cat == System.Globalization.UnicodeCategory.Format // bidi overrides, zero-width chars
                    || cat == System.Globalization.UnicodeCategory.LineSeparator
                    || cat == System.Globalization.UnicodeCategory.ParagraphSeparator)
                {
                    return url;
                }
            }

            return decoded;
        }

        private static readonly Regex TorRegex =
            new Regex(
                @"^http[s]?://[a-z2-7]{16}\.onion(/.*)?$",
                RegexOptions.Compiled |
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250));

        // Free-text fields are a few hundred characters at most; never run the pattern over
        // an arbitrarily large body (it backtracks badly on long hyphenated input).
        private const int MaxUrlScanLength = 5000;

        // Detects an actual link embedded in free text: an explicit scheme (http/https/ftp),
        // a "www." prefix, or a bare domain with a common TLD that is FOLLOWED BY A PATH
        // (e.g. "example.com/page"). The trailing "/..." requirement is deliberate: it stops
        // ordinary product names written in prose — "addy.io", "Proton.me", "Private.Storage" —
        // from being mis-flagged as links, while still catching pasted clickable URLs.
        private static readonly Regex UrlInTextRegex =
            new Regex(
                @"(?:https?://|ftp://|www\.)\S+" +
                @"|\b[a-z0-9][a-z0-9\-]*\.(?:com|net|org|io|co|xyz|info|me|biz|app|dev|site|online|tech|live|link|money|cash|exchange|finance|market|store|shop|page|gg|to|cc|ru|de|uk|nl|eu|onion|i2p)/\S*",
                RegexOptions.Compiled |
                RegexOptions.IgnoreCase);

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Check if it's a valid .onion link
            if (TorRegex.IsMatch(url))
            {
                return true;
            }

            // Check if it's a valid regular URL
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// True when the URL points at a Tor (.onion) or I2P (.i2p / .b32.i2p)
        /// hidden service. The main directory Link must be clearnet; hidden-service
        /// addresses belong in the alternate link fields.
        /// </summary>
        /// <returns></returns>
        public static bool IsOnionOrI2p(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var host = url.Trim();
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }

            host = host.ToLowerInvariant();
            return host.EndsWith(".onion", StringComparison.Ordinal)
                   || host.EndsWith(".i2p", StringComparison.Ordinal);
        }

        /// <summary>
        /// True when free text contains something that looks like a URL — an
        /// http(s)/ftp scheme, a "www." prefix, or a bare domain with a common TLD.
        /// Used to reject links pasted into free-text fields (Description, Note) where
        /// they don't belong; real links go in the dedicated Link fields.
        /// </summary>
        public static bool ContainsUrl(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.Length > MaxUrlScanLength)
            {
                text = text.Substring(0, MaxUrlScanLength);
            }

            try
            {
                return UrlInTextRegex.IsMatch(text);
            }
            catch (RegexMatchTimeoutException)
            {
                // Input that stalls the matcher is treated as containing a link, so it is rejected.
                return true;
            }
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty.", nameof(url));
            }

            // Remove multiple slashes (except for the protocol part)
            string normalizedUrl = Regex.Replace(url, "(?<!:)/{2,}", "/");

            // Remove the ending slash if present
            if (normalizedUrl.EndsWith("/"))
            {
                normalizedUrl = normalizedUrl.TrimEnd('/');
            }

            return normalizedUrl;
        }

        public static string MakeFullUrl(string domain, string relativePath)
        {
            // ensure the domain has no trailing slash before building
            var baseUri = new Uri(domain.TrimEnd('/'), UriKind.Absolute);

            // combine domain + path
            var fullUri = new Uri(baseUri, relativePath);

            // AbsoluteUri gives you the normalized URL, then TrimEnd removes any trailing slash
            return fullUri.AbsoluteUri.TrimEnd('/');
        }

        /// <summary>
        /// The lower-cased host of a URL (www. stripped), or null if it can't be parsed.
        /// </summary>
        public static string? TryGetHost(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var candidate = url.Trim();

            // Uri needs a scheme; add one so bare "example.com/x" parses.
            if (!candidate.Contains("://", StringComparison.Ordinal))
            {
                candidate = "https://" + candidate;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host.Substring(4);
            }

            return host;
        }

        /// <summary>
        /// True when two URLs are on the same site — same host, or one host is a subdomain of
        /// the other (e.g. "pay.example.com" vs "example.com", or a page on the same .onion).
        /// The leading-dot comparison means "evilexample.com" does NOT match "example.com".
        /// Used to require that a proof/acceptance link lives on the reviewed site itself.
        /// </summary>
        public static bool HostsRelated(string? urlA, string? urlB)
        {
            var a = TryGetHost(urlA);
            var b = TryGetHost(urlB);

            if (a is null || b is null)
            {
                return false;
            }

            return a == b
                || a.EndsWith("." + b, StringComparison.Ordinal)
                || b.EndsWith("." + a, StringComparison.Ordinal);
        }
    }
}