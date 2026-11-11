using System.Text.RegularExpressions;

namespace DirectoryManager.SubmissionReviewer.Text
{
    /// <summary>
    /// Finds the business's own social and messenger profiles (X/Twitter, Telegram, Discord, ...)
    /// in a page or in the site's own script files (many menus/footers are built by JavaScript,
    /// so the links only exist in the bundle). Share/intent buttons are ignored, and post URLs are
    /// trimmed to the profile. Pure functions; the reviewer fetches the content.
    /// </summary>
    public static class SocialLinkScan
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private static readonly Regex UrlRx = new Regex(
            @"https?://(?:www\.|mobile\.)?(?<host>x\.com|twitter\.com|t\.me|telegram\.me|discord\.gg|discord\.com|matrix\.to|simplex\.chat|signal\.me|youtube\.com|instagram\.com|facebook\.com|linkedin\.com|reddit\.com|github\.com|bsky\.app|mastodon\.social|primal\.net|njump\.me|tiktok\.com)(?<path>/[A-Za-z0-9_@%#!:/.\-+=~]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

        private static readonly HashSet<string> MessengerHosts = new (StringComparer.OrdinalIgnoreCase)
        {
            "t.me", "telegram.me", "discord.gg", "discord.com", "matrix.to", "simplex.chat", "signal.me",
        };

        // First path segments that are site features, not someone's profile.
        private static readonly HashSet<string> Reserved = new (StringComparer.OrdinalIgnoreCase)
        {
            "intent", "share", "sharer", "sharer.php", "i", "home", "search", "hashtag", "login", "signup", "explore",
            "privacy", "tos", "about", "settings", "messages", "notifications", "watch", "embed", "shorts", "dialog",
            "plugins", "legal", "help", "policies", "terms", "download", "apps", "joinchat",
        };

        /// <summary>(Social, Messenger) profile URLs found in <paramref name="content"/>, page order, at most 4 each.</summary>
        public static (IReadOnlyList<string> Social, IReadOnlyList<string> Messenger) Find(params string?[] content)
        {
            var social = new List<string>();
            var messenger = new List<string>();
            foreach (var text in content)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                MatchCollection matches;
                try
                {
                    matches = UrlRx.Matches(text);
                }
                catch (RegexMatchTimeoutException)
                {
                    continue;
                }

                foreach (Match m in matches)
                {
                    var url = Profile(m.Groups["host"].Value.ToLowerInvariant(), m.Groups["path"].Value);
                    if (url == null)
                    {
                        continue;
                    }

                    var list = MessengerHosts.Contains(m.Groups["host"].Value) ? messenger : social;
                    if (list.Count < 4 && !list.Contains(url, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(url);
                    }
                }
            }

            return (social, messenger);
        }

        private static string? Profile(string host, string path)
        {
            var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0 || Reserved.Contains(segs[0]) || segs[0].Contains("share", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (host == "twitter.com")
            {
                host = "x.com";
            }

            var first = segs[0].TrimEnd('.', ':', '!');
            if (first.Length < 2 || (first.Contains('.') && host is "x.com" or "t.me" or "instagram.com" or "github.com"))
            {
                return null;
            }

            // Platforms whose profile is two segments deep.
            var keep = host switch
            {
                "discord.com" => segs[0].Equals("invite", StringComparison.OrdinalIgnoreCase) && segs.Length > 1 ? 2 : 0,
                "linkedin.com" or "reddit.com" or "bsky.app" or "simplex.chat" => Math.Min(2, segs.Length),
                "matrix.to" => segs.Length,
                "youtube.com" => segs[0].StartsWith('@') ? 1 : Math.Min(2, segs.Length),
                _ => 1,
            };

            return keep == 0 ? null : $"https://{host}/" + string.Join("/", segs.Take(keep).Select(s => s.TrimEnd('.', ':', '!')));
        }
    }
}
