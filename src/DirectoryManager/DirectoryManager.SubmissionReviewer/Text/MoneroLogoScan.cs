using System.Net;
using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Finds a Monero logo on a page. Many sites show the coins they take only as icons, so the
    /// page text never says "Monero". This looks at the markup instead: images, inline SVGs, icon
    /// classes and CSS backgrounds whose file name, alt text, title, label or class names Monero/XMR,
    /// and image file names in the site's own scripts (pages built by JavaScript).
    /// Pure functions; the reviewer does the fetching. It reads names, it does not look at pixels.
    /// </summary>
    public static class MoneroLogoScan
    {
        private const RegexOptions Opts = RegexOptions.Singleline | RegexOptions.CultureInvariant;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        // "monero" anywhere, or "xmr" as its own token (xmr.svg, icon-xmr, XMR, xmrLogo; not "boxmrk").
        private static readonly Regex TokenRx = new Regex(
            "[Mm][Oo][Nn][Ee][Rr][Oo]|(?<![A-Za-z])(?:xmr|XMR|Xmr)(?![a-z])", Opts, Timeout);

        private static readonly Regex DropRx = new Regex(
            @"<(script|style|noscript|template)\b[^>]*>.*?</\1\s*>|<!--.*?-->", Opts | RegexOptions.IgnoreCase, Timeout);

        private static readonly Regex ImgRx = new Regex(
            @"<(?:img|source|image|object|embed)\b[^>]*>", Opts | RegexOptions.IgnoreCase, Timeout);

        private static readonly Regex SvgRx = new Regex(
            @"<svg\b(?<attrs>[^>]{0,2000})>(?<body>.{0,6000}?)</svg\s*>", Opts | RegexOptions.IgnoreCase, Timeout);

        private static readonly Regex SvgNameRx = new Regex(
            @"<(?:title|desc)\b[^>]*>(?<t>[^<]{1,120})</(?:title|desc)\s*>|<use\b[^>]*?href\s*=\s*[""'](?<t>[^""']{1,200})[""']",
            Opts | RegexOptions.IgnoreCase, Timeout);

        // Icon fonts and sprites: <i class="cc XMR">, <span class="crypto-icon icon-monero">.
        private static readonly Regex IconRx = new Regex(
            @"<(?:i|span|div|a|li|button|label|figure|picture)\b[^>]{0,1500}>", Opts | RegexOptions.IgnoreCase, Timeout);

        private static readonly Regex AttrRx = new Regex(
            "(?<name>[a-zA-Z:_-]+)\\s*=\\s*(?:\"(?<v>[^\"]*)\"|'(?<v>[^']*)')", Opts, Timeout);

        private static readonly Regex CssUrlRx = new Regex(
            @"url\(\s*[""']?(?<u>[^""')]{1,300})", Opts | RegexOptions.IgnoreCase, Timeout);

        // An image file named after Monero inside a script or style sheet: "/img/coins/xmr.svg".
        private static readonly Regex AssetRx = new Regex(
            @"(?<u>[\w\-./:@%~]{0,160}\.(?:svg|png|webp|avif|gif|jpe?g|ico))(?![\w])", Opts | RegexOptions.IgnoreCase, Timeout);

        private static readonly string[] ImageAttrs = { "src", "data-src", "srcset", "data-srcset", "href", "xlink:href", "data", "alt", "title", "aria-label", "class", "id" };
        private static readonly string[] IconAttrs = { "class", "style", "aria-label", "title", "data-icon", "data-coin", "data-currency" };

        /// <summary>
        /// Evidence lines for Monero logos in the page markup, e.g.
        /// <c>image: src=/img/coins/xmr.svg alt="Monero"</c>. Empty when none is found.
        /// </summary>
        public static IReadOnlyList<string> InHtml(string? html, int max = 4)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(html))
            {
                return found;
            }

            try
            {
                var clean = DropRx.Replace(html, " ");

                foreach (Match m in ImgRx.Matches(clean))
                {
                    var attrs = Attrs(m.Value);
                    var hit = ImageAttrs.Where(a => attrs.TryGetValue(a, out var v) && Names(v)).ToList();
                    if (hit.Count > 0)
                    {
                        Add(found, "image: " + string.Join(" ", hit.Select(a => $"{a}={Show(attrs[a])}")));
                    }
                }

                foreach (Match m in SvgRx.Matches(clean))
                {
                    var attrs = Attrs(m.Groups["attrs"].Value);
                    var label = new[] { "aria-label", "class", "id", "title", "data-icon" }
                        .Where(a => attrs.TryGetValue(a, out var v) && Names(v))
                        .Select(a => $"{a}={Show(attrs[a])}")
                        .ToList();
                    foreach (Match n in SvgNameRx.Matches(m.Groups["body"].Value))
                    {
                        if (Names(n.Groups["t"].Value))
                        {
                            label.Add("names " + Show(n.Groups["t"].Value));
                        }
                    }

                    if (label.Count > 0)
                    {
                        Add(found, "inline SVG icon: " + string.Join(" ", label.Distinct()));
                    }
                }

                foreach (Match m in IconRx.Matches(clean))
                {
                    var attrs = Attrs(m.Value);
                    foreach (var a in IconAttrs)
                    {
                        if (!attrs.TryGetValue(a, out var v))
                        {
                            continue;
                        }

                        if (a == "style")
                        {
                            foreach (Match u in CssUrlRx.Matches(v))
                            {
                                if (Names(u.Groups["u"].Value))
                                {
                                    Add(found, "CSS background image: " + Show(u.Groups["u"].Value));
                                }
                            }
                        }
                        else if (Names(v) && (a != "class" || IsIconClass(v)))
                        {
                            Add(found, $"icon element: {a}={Show(v)}");
                        }
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological page: keep what was found so far.
            }

            return found.Take(max).ToList();
        }

        /// <summary>Image files named after Monero in a script or style sheet (distinct).</summary>
        public static IReadOnlyList<string> AssetsInScript(string? script, int max = 3)
        {
            var found = new List<string>();
            if (string.IsNullOrEmpty(script))
            {
                return found;
            }

            try
            {
                foreach (Match m in AssetRx.Matches(script))
                {
                    var url = m.Groups["u"].Value;
                    var file = url[(url.LastIndexOf('/') + 1)..];
                    if (Names(file))
                    {
                        Add(found, "image file: " + Show(url));
                        if (found.Count >= max)
                        {
                            break;
                        }
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
            }

            return found;
        }

        // Embedded image data (data: URIs) is random letters and can spell "XMR" by chance: never read it.
        private static bool Names(string? value) =>
            !string.IsNullOrEmpty(value) && !value.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase) && TokenRx.IsMatch(value);

        // A class list is an icon only when it reads like one: "cc XMR", "icon-monero", "coin xmr",
        // not a layout class on a Monero-themed site ("monero-section-wrapper").
        private static bool IsIconClass(string classes) =>
            Regex.IsMatch(classes, @"\b(?:icon|ico|logo|coin|crypto|currency|cc|cf|payment|pay|badge|symbol|sprite)\b|(?:icon|ico|logo|coin|symbol)[-_]|[-_](?:icon|ico|logo|coin|symbol)\b", RegexOptions.IgnoreCase, Timeout);

        private static Dictionary<string, string> Attrs(string tag)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match a in AttrRx.Matches(tag))
            {
                d.TryAdd(a.Groups["name"].Value, WebUtility.HtmlDecode(a.Groups["v"].Value));
            }

            return d;
        }

        private static string Show(string value)
        {
            var v = Regex.Replace(value, @"\s+", " ").Trim();
            if (v.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                v = "(embedded image data)";
            }

            return "\"" + (v.Length <= 110 ? v : v[..110] + "…") + "\"";
        }

        private static void Add(List<string> list, string line)
        {
            if (!list.Contains(line, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(line);
            }
        }
    }
}
