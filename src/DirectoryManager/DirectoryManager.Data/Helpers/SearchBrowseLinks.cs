using System.Text.RegularExpressions;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Links shown above search results: the category/subcategory/tag/country page a search
    /// names ("vpn" → VPNs &amp; Proxies, "gift cards" → Gift Cards), shortcuts to site pages
    /// ("advertise", "scam"), and the closest categories when a search finds nothing.
    /// Pure: the caller passes the browsable pages in.
    /// </summary>
    public static class SearchBrowseLinks
    {
        public const int MaxLinks = 3;

        /// <summary>
        /// Site-feature searches seen in the search log (120 days to 2026-11-13: scam 13,
        /// advertise/advertising/advertisement 5, review 2). Keys are whole folded queries.
        /// </summary>
        private static readonly Dictionary<string, BrowseTarget> Shortcuts = BuildShortcuts();

        private static readonly Regex Parenthetical = new (@"\([^)]*\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex WordSplit = new (@"[^\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public enum TargetKind
        {
            Category = 0,
            Subcategory = 1,
            Tag = 2,
            Country = 3,
            Page = 4,
        }

        /// <summary>
        /// Pages whose name the search names: every search word (or an equivalent, see
        /// <see cref="SearchSynonyms.Equivalents"/>) is a word of one part of the name (parts are
        /// split at "&amp;" and "/", text in brackets ignored), and that part has at most one
        /// extra word ("sms" → "SMS Verification &amp; Virtual Numbers"). Exact parts first,
        /// then categories before subcategories before tags.
        /// </summary>
        public static List<BrowseTarget> FindNamed(string query, IEnumerable<BrowseTarget> targets, int max = MaxLinks)
        {
            var q = DirectorySearchEngine.Parse(query);
            var result = new List<(BrowseTarget Target, int Extra)>();

            // "e-sim" / "e sim" → "esim", as the search itself does.
            var words = new List<string>();
            for (int i = 0; i < q.Words.Count; i++)
            {
                bool join = q.Words[i].Length == 1 && i + 1 < q.Words.Count;
                words.Add(join ? q.Words[i] + q.Words[++i] : q.Words[i]);
            }

            if (words.Count > 0)
            {
                var wanted = words.Select(Alternatives).ToList();
                foreach (var t in targets.Where(t => t.Kind != TargetKind.Country && t.Kind != TargetKind.Page))
                {
                    int bestExtra = int.MaxValue;
                    foreach (var part in Parts(t.Name))
                    {
                        if (wanted.All(alts => part.Any(alts.Contains)) && part.Length - words.Count <= 1)
                        {
                            bestExtra = Math.Min(bestExtra, part.Length - words.Count);
                        }
                    }

                    if (bestExtra != int.MaxValue)
                    {
                        result.Add((t, bestExtra));
                    }
                }
            }
            else if (q.CountryCode != null)
            {
                var country = targets.FirstOrDefault(t => t.Kind == TargetKind.Country
                    && string.Equals(t.Name, q.CountryCode, StringComparison.OrdinalIgnoreCase));
                if (country != null)
                {
                    result.Add((country, 0));
                }
            }

            return result
                .OrderBy(r => r.Extra)
                .ThenBy(r => r.Target.Kind)
                .ThenBy(r => r.Target.Label, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Target)
                .DistinctBy(t => t.Path)
                .Take(max)
                .ToList();
        }

        /// <summary>
        /// For a search with no results: categories/subcategories sharing a word (or the start of
        /// one, 4+ letters) with the search. Empty when nothing is close.
        /// </summary>
        public static List<BrowseTarget> FindClosest(string query, IEnumerable<BrowseTarget> targets, int max = 4)
        {
            var q = DirectorySearchEngine.Parse(query);
            var words = q.Words.Where(w => w.Length >= 3).SelectMany(Alternatives).Distinct().ToList();
            if (words.Count == 0)
            {
                return new List<BrowseTarget>();
            }

            return targets
                .Where(t => t.Kind is TargetKind.Category or TargetKind.Subcategory)
                .Select(t => (Target: t, Hits: Parts(t.Name).SelectMany(p => p).Count(nw => words.Any(w =>
                    nw == w || (w.Length >= 4 && nw.StartsWith(w, StringComparison.Ordinal))
                    || (nw.Length >= 4 && w.StartsWith(nw, StringComparison.Ordinal))))))
                .Where(x => x.Hits > 0)
                .OrderByDescending(x => x.Hits)
                .ThenBy(x => x.Target.Kind)
                .ThenBy(x => x.Target.Label, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Target)
                .DistinctBy(t => t.Path)
                .Take(max)
                .ToList();
        }

        /// <summary>A site page for a navigational search ("advertise", "scam"), or null.</summary>
        public static BrowseTarget? FindShortcut(string query)
        {
            var key = string.Join(' ', WordSplit.Split(DirectorySearchEngine.Fold((query ?? string.Empty).ToLowerInvariant())).Where(w => w.Length > 0));
            return Shortcuts.TryGetValue(key, out var target) ? target : null;
        }

        // A query word plus its stem and equivalents ("casino" → casino, gambling, betting).
        private static HashSet<string> Alternatives(string word)
        {
            var set = new HashSet<string>(StringComparer.Ordinal) { word, DirectorySearchEngine.Stem(word) };
            if (SearchSynonyms.Equivalents.TryGetValue(word, out var eq))
            {
                foreach (var e in eq)
                {
                    set.Add(e);
                    set.Add(DirectorySearchEngine.Stem(e));
                }
            }

            return set;
        }

        // Stemmed words of each part: "eSIMs & SIM Cards" → [esim], [sim, card]; "Instant Swaps (No Account)" → [instant, swap].
        private static IEnumerable<string[]> Parts(string name)
        {
            var clean = Parenthetical.Replace(DirectorySearchEngine.Fold((name ?? string.Empty).ToLowerInvariant()), " ");
            foreach (var part in clean.Split('&', '/', ','))
            {
                var words = WordSplit.Split(part)
                    .Where(w => w.Length > 0 && w != "and")
                    .Select(DirectorySearchEngine.Stem)
                    .ToArray();
                if (words.Length > 0)
                {
                    yield return words;
                }
            }
        }

        private static Dictionary<string, BrowseTarget> BuildShortcuts()
        {
            var advertise = new BrowseTarget("Advertise: sponsor a listing", "/advertise", TargetKind.Page, "advertise");
            var submit = new BrowseTarget("Submit a listing", "/submit", TargetKind.Page, "submit");
            var incidents = new BrowseTarget("Open incidents (listings marked Scam or Questionable)", "/incidents", TargetKind.Page, "incidents");
            var reviews = new BrowseTarget("Latest reviews", "/reviews", TargetKind.Page, "reviews");
            var contact = new BrowseTarget("Contact", "/contact", TargetKind.Page, "contact");
            var faq = new BrowseTarget("FAQ", "/faq", TargetKind.Page, "faq");

            return new Dictionary<string, BrowseTarget>(StringComparer.Ordinal)
            {
                ["advertise"] = advertise,
                ["advertising"] = advertise,
                ["advertisement"] = advertise,
                ["advertize"] = advertise,
                ["sponsor"] = advertise,
                ["sponsorship"] = advertise,
                ["sponsored"] = advertise,
                ["submit"] = submit,
                ["submission"] = submit,
                ["add listing"] = submit,
                ["add my site"] = submit,
                ["add site"] = submit,
                ["suggest a listing"] = submit,
                ["list my business"] = submit,
                ["scam"] = incidents,
                ["scams"] = incidents,
                ["scammer"] = incidents,
                ["incident"] = incidents,
                ["incidents"] = incidents,
                ["review"] = reviews,
                ["reviews"] = reviews,
                ["contact"] = contact,
                ["contact us"] = contact,
                ["faq"] = faq,
            };
        }
    }
}
