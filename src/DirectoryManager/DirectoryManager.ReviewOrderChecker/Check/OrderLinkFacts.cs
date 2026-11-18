using System.Text.RegularExpressions;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    /// <summary>
    /// Facts about an order link that are worked out in code, not by the AI: is it on the
    /// listing's own site, what could be typed in to unlock it, how to compare two links.
    /// </summary>
    public static partial class OrderLinkFacts
    {
        // Hosts where a "proof" is a picture, a post or a chain record, never an order page.
        private static readonly string[] NonOrderHosts =
        {
            "imgur.com", "ibb.co", "postimg.cc", "x.com", "twitter.com", "t.me", "reddit.com",
            "blockchair.com", "mempool.space", "etherscan.io", "tronscan.org", "solscan.io",
            "bscscan.com", "xmrchain.net", "blockstream.info", "litecoinspace.org",
        };

        public static Uri? ParseHttpUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Reviewers sometimes paste two links or trailing words; the first token is the link.
            var first = value.Trim().Split(new[] { ' ', '\t', '\r', '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)[0];
            return Uri.TryCreate(first, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                   && uri.Host.Contains('.')
                ? uri
                : null;
        }

        /// <summary>The part of a host that identifies the site: "app.baltex.io" → "baltex.io"; an onion stays whole.</summary>
        public static string SiteKey(string host)
        {
            host = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
            if (host.EndsWith(".onion", StringComparison.Ordinal))
            {
                var onionLabels = host.Split('.');
                return onionLabels.Length >= 2 ? onionLabels[^2] + ".onion" : host;
            }

            var labels = host.Split('.');
            if (labels.Length <= 2)
            {
                return host;
            }

            // Two-part public suffixes such as co.uk / com.au.
            var second = labels[^2];
            var twoPart = labels[^1].Length == 2 && (second is "co" or "com" or "net" or "org" or "gov" or "ac");
            return string.Join('.', labels[^(twoPart ? 3 : 2)..]);
        }

        public static bool IsOnion(Uri uri) => uri.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);

        public static bool IsNonOrderHost(Uri uri) => NonOrderHosts.Contains(SiteKey(uri.Host));

        /// <summary>True when the order link is on one of the listing's own addresses.</summary>
        public static bool IsOnListingSite(Uri orderUrl, IEnumerable<string?> listingLinks)
        {
            var key = SiteKey(orderUrl.Host);
            foreach (var link in listingLinks)
            {
                var uri = ParseHttpUrl(link);
                if (uri != null && SiteKey(uri.Host) == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Same order link written two ways compares equal (case of host, trailing slash, fragment, www).</summary>
        public static string NormalizeForCompare(string? url)
        {
            var uri = ParseHttpUrl(url);
            if (uri == null)
            {
                return (url ?? string.Empty).Trim().ToLowerInvariant();
            }

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host[4..];
            }

            return host + uri.AbsolutePath.TrimEnd('/') + uri.Query;
        }

        /// <summary>
        /// Values from the reviewer's "verification details" that could unlock an order page:
        /// wallet addresses first, then other long codes. Links are left out.
        /// </summary>
        public static IReadOnlyList<string> UnlockCandidates(string? context)
        {
            if (string.IsNullOrWhiteSpace(context) || context.Contains("BEGIN PGP", StringComparison.Ordinal))
            {
                return Array.Empty<string>();
            }

            // Links are never typed into an order page; drop them whole so their parts are not picked up.
            context = Regex.Replace(context, @"\S+://\S+", " ");
            var found = new List<string>();
            foreach (Match m in TokenRegex().Matches(context))
            {
                var token = m.Value.Trim('.', ',', ';', ':', ')', '(', '"', '\'');
                if (token.Length < 12 || token.Length > 110
                    || token.Contains("://", StringComparison.Ordinal)
                    || token.Contains('/')
                    || !token.Any(char.IsDigit)
                    || !token.Any(char.IsLetter))
                {
                    continue;
                }

                if (!found.Contains(token))
                {
                    found.Add(token);
                }
            }

            // Addresses (26+ characters) are what order pages ask for; try those first.
            return found.OrderByDescending(t => t.Length >= 26).Take(3).ToList();
        }

        [GeneratedRegex(@"[A-Za-z0-9_\-]{12,110}")]
        private static partial Regex TokenRegex();
    }
}
