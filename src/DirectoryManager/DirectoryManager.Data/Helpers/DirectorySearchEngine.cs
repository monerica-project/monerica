using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Ranks directory listings for the public search box. Pure and in-memory (the whole
    /// directory is only a few thousand rows), so it can be unit-tested without a database.
    ///
    /// How a query is handled:
    /// 1. Normalized: lower case, accents folded ("trêvoid" = "trevoid"), whole-query aliases from
    ///    <see cref="SearchSynonyms.PhraseAliases"/> ("buy monero" → exchange), a lone letter joined
    ///    to the next word when that makes a known word ("e-sim" → esim), and an unknown squashed
    ///    word split into two known ones ("simcard" → sim card).
    /// 2. Words, not phrases: every meaningful word must match somewhere, in any order.
    ///    If nothing matches every word, listings matching the most words are shown.
    /// 3. Ordering follows <see cref="SearchRankingModel"/>: name matches first (exact name always
    ///    on top), then category/tag matches, then description mentions; within those, listing
    ///    quality (status, smoothed review rating, review count, problems, trust facts).
    /// 4. Words of 3 letters or fewer ("ai", "rdp", "tor") only match whole words.
    /// 5. A country word ("esim usa") narrows the other words' results to that country.
    /// 6. <see cref="SuggestCorrection"/> offers a spelling fix ("trocadr" → "trocador")
    ///    when a search finds nothing.
    /// 7. Scam listings are only shown when the search names them (or their domain), or when
    ///    nothing but scams matches (sunk and labeled, as a warning).
    /// </summary>
    public static class DirectorySearchEngine
    {
        // Field weights: how much a full-word match in each field is worth (used for the
        // relevance points within a tier; the tier itself comes from which field matched).
        private const double NameWeight = 100;
        private const double SubcategoryWeight = 45;
        private const double TagWeight = 40;
        private const double HostWeight = 35;
        private const double CategoryWeight = 30;
        private const double DescriptionWeight = 15;
        private const double LocationWeight = 10;
        private const double NoteWeight = 8;
        private const double ProcessorWeight = 8;

        // A listing whose subcategory is exactly what a generic search names ("Proxies" for
        // "proxy", not "Proxy Shopping") gets this many extra points.
        private const double ExactSubcategoryBonus = 15;

        // Extra score points for listings that fit a country / "no kyc" search.
        private const double CountryBonus = 10;
        private const double NoKycBonus = 6;

        // Match strengths within a field.
        private const double WordMatch = 1.0;
        private const double PrefixMatch = 0.7;
        private const double InsideWordMatch = 0.5;

        // A word found only through an equivalent ("casino" → Gambling) counts almost fully;
        // one found only through a related word is shown after all direct matches.
        private const double EquivalentWeight = 0.9;
        private const double RelatedWeight = 0.3;

        // Words only matched as whole words (prefix/inside-word matching disabled).
        private const int ShortWordMaxLength = 3;

        private static readonly Regex NoKycRegex = new (
            @"\b(no[\s-]?kyc|non[\s-]?kyc|without\s+kyc|kyc[\s-]?free|nokyc)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex WordSplit = new (
            @"[^\p{L}\p{N}]+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex DomainLike = new (
            @"^(https?://)?(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,24}(/.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Domain endings dropped when a listing's name is its domain ("SolvoCard.com" = "solvocard").
        private static readonly HashSet<string> CommonNameEndings = new (StringComparer.Ordinal)
        {
            "com", "net", "org", "io", "co", "xyz", "app", "me", "info", "biz",
        };

        // Dropped when the query has other words: they don't narrow anything in a
        // Monero-only directory ("buy esim", "monero vps").
        private static readonly HashSet<string> StopWords = new (StringComparer.Ordinal)
        {
            "a", "an", "the", "and", "or", "for", "with", "to", "in", "on", "of", "by", "at", "from",
            "buy", "buying", "pay", "paying", "use", "using", "accept", "accepts", "accepting",
            "get", "find", "best", "cheap", "site", "sites", "website", "websites", "service", "services",
            "monero", "xmr", "crypto", "cryptocurrency", "cryptocurrencies",
        };

        internal enum FieldClass
        {
            None = 0,
            Text = 1,
            Category = 2,
            Taxonomy = 3,
            NameInside = 4,
            NameWhole = 5,
        }

        public sealed class ParsedQuery
        {
            public string Normalized { get; init; } = string.Empty;

            public string Compact { get; init; } = string.Empty;

            public IReadOnlyList<string> Words { get; init; } = Array.Empty<string>();

            public string? CountryCode { get; init; }

            public IReadOnlyList<string> CountryTerms { get; init; } = Array.Empty<string>();

            /// <summary>The country word(s) as typed ("uk"), used to rank listings named with it.</summary>
            public string? CountryPhrase { get; init; }

            public bool NoKyc { get; init; }

            public string? Host { get; init; }

            internal ParsedQuery WithWords(IReadOnlyList<string> words) => new ()
            {
                Normalized = this.Normalized,
                Compact = this.Compact,
                Words = words,
                CountryCode = this.CountryCode,
                CountryTerms = this.CountryTerms,
                CountryPhrase = this.CountryPhrase,
                NoKyc = this.NoKyc,
                Host = this.Host,
            };
        }

        /// <summary>
        /// A spelling correction. <see cref="AutoApply"/> is true for clear typos (a missing,
        /// extra or swapped letter: "trocadr" → "trocador"), where showing the corrected results
        /// straight away is safe. A changed letter can turn one real word into another
        /// ("mixer" → "miner"), so those are only offered as "Did you mean…?".
        /// </summary>
        public sealed class Correction
        {
            public Correction(string query, bool autoApply)
            {
                this.Query = query;
                this.AutoApply = autoApply;
            }

            public string Query { get; }

            public bool AutoApply { get; }
        }

        /// <summary>One ranked result and why it is where it is (shown to admins with ?debug=1).</summary>
        public sealed class RankedResult
        {
            public DirectoryEntry Entry { get; init; } = null!;

            public SearchMatchTier Tier { get; init; }

            public bool Sunk { get; init; }

            public int Protection { get; init; }

            public int MatchedWords { get; init; }

            public double Relevance { get; init; }

            public double Bonus { get; init; }

            public ListingQuality Quality { get; init; } = null!;

            public bool ViaRelatedWord { get; init; }

            public double Score => SearchRankingModel.Band(this.Tier) + this.Relevance + this.Bonus + this.Quality.Total;

            public string Explain() =>
                $"{this.Tier}{(this.Protection > 0 ? " (name, protected)" : string.Empty)}{(this.Sunk ? " SUNK" : string.Empty)}{(this.ViaRelatedWord ? " via related word" : string.Empty)}; " +
                $"score {this.Score:0.0} = band {SearchRankingModel.Band(this.Tier):0} + relevance {this.Relevance:0.0} + bonus {this.Bonus:0} + {this.Quality}";
        }

        /// <summary>
        /// The searchable directory: each listing split into words once, plus its quality points.
        /// Build it once and reuse it for many searches (it never changes the entries).
        /// </summary>
        public sealed class SearchIndex
        {
            internal SearchIndex(List<Doc> docs, Dictionary<int, ListingQuality> quality, bool alwaysShowScams)
            {
                this.Docs = docs;
                this.Quality = quality;
                this.AlwaysShowScams = alwaysShowScams;

                foreach (var d in docs)
                {
                    foreach (var w in d.NameWords.Concat(d.TagWords).Concat(d.SubcategoryWords).Concat(d.CategoryWords))
                    {
                        this.KnownWords.Add(w);
                        this.KnownWords.Add(Stem(w));
                    }

                    foreach (var w in d.TagWords.Concat(d.SubcategoryWords).Concat(d.CategoryWords))
                    {
                        this.TaxonomyWords.Add(w);
                        this.TaxonomyWords.Add(Stem(w));
                    }

                    foreach (var field in new[] { d.SubcategoryWords, d.CategoryWords })
                    {
                        for (int i = 0; i + 1 < field.Length; i++)
                        {
                            this.TaxonomyPairs.Add(Stem(field[i]) + " " + Stem(field[i + 1]));
                        }
                    }

                    if (SinkTier(d.Entry.DirectoryStatus) == 0)
                    {
                        foreach (var w in d.NameWords.Concat(d.TagWords).Concat(d.SubcategoryWords).Concat(d.CategoryWords).Concat(d.HostLabels))
                        {
                            if (w.Length >= 3)
                            {
                                this.Vocabulary[w] = this.Vocabulary.GetValueOrDefault(w) + 1;
                            }
                        }
                    }
                }
            }

            public int Count => this.Docs.Count;

            public DateTime BuiltUtc { get; } = DateTime.UtcNow;

            internal List<Doc> Docs { get; }

            internal Dictionary<int, ListingQuality> Quality { get; }

            internal bool AlwaysShowScams { get; }

            /// <summary>Words of names, tags, subcategories and categories (plus stems).</summary>
            internal HashSet<string> KnownWords { get; } = new (StringComparer.Ordinal);

            /// <summary>Words of tags, subcategories and categories (plus stems).</summary>
            internal HashSet<string> TaxonomyWords { get; } = new (StringComparer.Ordinal);

            /// <summary>Adjacent word pairs of subcategory/category names, stemmed ("sim card", "gift card").</summary>
            internal HashSet<string> TaxonomyPairs { get; } = new (StringComparer.Ordinal);

            /// <summary>Spelling vocabulary from non-scam listings, with how often each word appears.</summary>
            internal Dictionary<string, int> Vocabulary { get; } = new (StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the search index. <paramref name="alwaysShowScams"/> is for admin/duplicate
        /// lookups, where Scam listings must be findable by any word.
        /// </summary>
        public static SearchIndex BuildIndex(IEnumerable<DirectoryEntry> entries, SearchSignals? signals = null, bool alwaysShowScams = false)
        {
            var list = entries.ToList();
            var docs = Expand(list).Select(e => new Doc(e)).ToList();
            var quality = new Dictionary<int, ListingQuality>();
            foreach (var e in list)
            {
                quality[e.DirectoryEntryId] = SearchRankingModel.Compute(e, signals ?? SearchSignals.None);
            }

            return new SearchIndex(docs, quality, alwaysShowScams);
        }

        /// <summary>Returns the matching listings, best match first.</summary>
        public static List<DirectoryEntry> Rank(IEnumerable<DirectoryEntry> entries, string query) =>
            Rank(BuildIndex(entries), query);

        public static List<DirectoryEntry> Rank(SearchIndex index, string query) =>
            RankExplained(index, query).Select(r => r.Entry).ToList();

        /// <summary>Like <see cref="Rank(SearchIndex, string)"/>, with each result's ranking details.</summary>
        public static List<RankedResult> RankExplained(SearchIndex index, string query)
        {
            var q = Parse(query);
            if (q.Words.Count == 0 && q.CountryCode == null && !q.NoKyc && q.Host == null)
            {
                return new List<RankedResult>();
            }

            var docs = index.Docs;

            // Domain search ("xmr.cards"): if a listing is on that domain, show just those.
            if (q.Host != null)
            {
                var hostDocs = docs.Where(d => d.HostMatch(q.Host) > 0).ToList();
                if (hostDocs.Count > 0)
                {
                    return Order(
                        hostDocs.Select(d => new Hit(d, SearchMatchTier.Taxonomy, 1, 0, d.HostMatch(q.Host) * 10)),
                        q,
                        index,
                        namedByDomain: true);
                }
            }

            q = RewriteWords(q, index);
            bool generic = IsGenericQuery(q, index);

            var ranked = RankDocs(index, q, relaxShortWords: false, generic);

            // A short word that matched nothing as a whole word ("bit") is probably the start of
            // a name: retry letting it match the beginning of listing names (Bitania, BitcoinVN).
            if (ranked.Count == 0 && q.Words.Any(w => w.Length <= ShortWordMaxLength))
            {
                ranked = RankDocs(index, q, relaxShortWords: true, generic);
            }

            // Scam listings that match (e.g. a scam network named in a scam's note) are shown,
            // sunk and labeled, when nothing else matches at all.
            if (ranked.Count == 0)
            {
                ranked = RankDocs(index, q, relaxShortWords: q.Words.Any(w => w.Length <= ShortWordMaxLength), generic, showScams: true);
            }

            // Then listings that only match through a related word ("sim" → eSIM), always after
            // every direct match so the word people typed still wins.
            if (q.Words.Any(w => SearchSynonyms.Related.ContainsKey(w)))
            {
                var seen = new HashSet<int>(ranked.Select(r => r.Entry.DirectoryEntryId));
                var extra = RankDocs(index, q, relaxShortWords: false, generic, useRelated: true)
                    .Where(r => seen.Add(r.Entry.DirectoryEntryId))
                    .Select(r => new RankedResult
                    {
                        Entry = r.Entry,
                        Tier = r.Tier,
                        Sunk = r.Sunk,
                        Protection = r.Protection,
                        MatchedWords = r.MatchedWords,
                        Relevance = r.Relevance,
                        Bonus = r.Bonus,
                        Quality = r.Quality,
                        ViaRelatedWord = true,
                    })
                    .ToList();

                // Keep sunk listings at the very end: insert before the trailing sunk run.
                int insertAt = ranked.Count;
                while (insertAt > 0 && ranked[insertAt - 1].Sunk)
                {
                    insertAt--;
                }

                ranked.InsertRange(insertAt, extra.Where(e => !e.Sunk));
                ranked.AddRange(extra.Where(e => e.Sunk));
            }

            return ranked;
        }

        /// <summary>
        /// When a search finds nothing, returns the query with misspelled words replaced by the
        /// closest word used in listing names, tags or categories ("trocadr" → "trocador"), or by
        /// the closest part of a squashed listing name ("bazar" → "bazaar" in XMRBazaar); null if
        /// there's no close match. Scam and Questionable listings are never suggested.
        /// </summary>
        public static Correction? SuggestCorrection(IEnumerable<DirectoryEntry> entries, string query) =>
            SuggestCorrection(BuildIndex(entries), query);

        public static Correction? SuggestCorrection(SearchIndex index, string query)
        {
            var q = Parse(query);
            if (q.Words.Count == 0)
            {
                return null;
            }

            var docs = index.Docs;
            var vocab = index.Vocabulary;

            bool changed = false;
            bool autoApply = true;
            var corrected = q.Normalized;
            foreach (var word in q.Words)
            {
                bool known = word.Length < 4 || docs.Any(d => d.ScoreWord(word, relaxShortWords: false).Score > 0);
                if (known)
                {
                    continue;
                }

                // Damerau-Levenshtein (optimal string alignment): 1 edit for 4-7 letters, 2 for 8+.
                int maxDistance = word.Length <= 7 ? 1 : 2;
                var best = vocab
                    .Select(kv => (Word: kv.Key, Count: kv.Value, Distance: EditDistance(word, kv.Key, maxDistance)))
                    .Where(x => x.Distance <= maxDistance)
                    .OrderBy(x => x.Distance)
                    .ThenByDescending(x => x.Count)
                    .ThenBy(x => x.Word, StringComparer.Ordinal)
                    .FirstOrDefault();

                string? replacement = best.Word;

                // Not a word anywhere: compare with whole squashed names, and with names minus a
                // leading/trailing stop word ("globincard" → goblincard, "bazar" → bazaar from XMRBazaar).
                if (replacement == null && word.Length >= 5)
                {
                    replacement = docs
                        .Where(d => SinkTier(d.Entry.DirectoryStatus) == 0)
                        .SelectMany(d => NameVariants(d))
                        .Distinct()
                        .Select(v => (Text: v, Distance: EditDistance(word, v, maxDistance)))
                        .Where(x => x.Distance <= maxDistance && x.Distance > 0)
                        .OrderBy(x => x.Distance)
                        .ThenBy(x => x.Text, StringComparer.Ordinal)
                        .Select(x => x.Text)
                        .FirstOrDefault();
                }

                if (replacement != null)
                {
                    corrected = Regex.Replace(
                        corrected,
                        $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])",
                        replacement);
                    changed = true;
                    autoApply &= !IsSubstitutionOnly(word, replacement);
                }
            }

            return changed && corrected != q.Normalized ? new Correction(corrected, autoApply) : null;
        }

        public static ParsedQuery Parse(string? query)
        {
            var normalized = Regex.Replace(Fold((query ?? string.Empty).Trim().ToLowerInvariant()), @"\s+", " ");

            string? host = null;
            if (!normalized.Contains(' ') && DomainLike.IsMatch(normalized))
            {
                host = ExtractHost(normalized);
            }

            bool noKyc = NoKycRegex.IsMatch(normalized);
            var rest = noKyc ? NoKycRegex.Replace(normalized, " ") : normalized;

            // Whole-query aliases ("buy monero" → exchange, "credit card" → prepaid card).
            if (host == null)
            {
                var key = string.Join(' ', WordSplit.Split(rest).Where(w => w.Length > 0));
                if (SearchSynonyms.PhraseAliases.TryGetValue(key, out var alias))
                {
                    rest = alias;
                }
            }

            // A domain is searched by its links, and by its name words ("xmr.cards" → "xmr cards").
            var words = host != null
                ? host.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1)
                    .SelectMany(p => p.Split('-', StringSplitOptions.RemoveEmptyEntries)).ToList()
                : WordSplit.Split(rest).Where(w => w.Length > 0).ToList();

            // Country words: longest phrase first ("united states", then "usa"). The country
            // words are removed; if they were the whole query, the country filter alone applies.
            string? country = null;
            string? countryPhrase = null;
            if (host == null)
            {
                for (int len = Math.Min(3, words.Count); len >= 1 && country == null; len--)
                {
                    for (int i = 0; i + len <= words.Count; i++)
                    {
                        var phrase = string.Join(' ', words.Skip(i).Take(len));
                        if (TryCountry(phrase, out var code))
                        {
                            country = code;
                            countryPhrase = phrase;
                            words.RemoveRange(i, len);
                            break;
                        }
                    }
                }
            }

            var meaningful = words.Where(w => !StopWords.Contains(w)).ToList();
            if (meaningful.Count > 0)
            {
                words = meaningful;
            }
            else if (noKyc || country != null)
            {
                // e.g. "no kyc monero" — the filters carry the meaning.
                words = new List<string>();
            }

            return new ParsedQuery
            {
                Normalized = normalized,
                Compact = Compact(normalized),
                Words = words.Distinct().ToList(),
                CountryCode = country,
                CountryTerms = country != null ? CountryHelper.GetCountryTerms(country) : Array.Empty<string>(),
                CountryPhrase = countryPhrase,
                NoKyc = noKyc,
                Host = host,
            };
        }

        /// <summary>Lower-case text with accents removed ("trêvoid" → "trevoid", Turkish "ı" → "i").</summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text) || text.All(c => c < 128))
            {
                return text;
            }

            var sb = new StringBuilder(text.Length);
            foreach (var c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(c switch
                {
                    'ı' => 'i',
                    'ł' => 'l',
                    'ø' => 'o',
                    'đ' => 'd',
                    _ => c,
                });
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        internal static string Stem(string word)
        {
            if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
            {
                return word[..^3] + "y";
            }

            if (word.Length > 4 && (word.EndsWith("ses", StringComparison.Ordinal) || word.EndsWith("xes", StringComparison.Ordinal)
                || word.EndsWith("ches", StringComparison.Ordinal) || word.EndsWith("shes", StringComparison.Ordinal)))
            {
                return word[..^2];
            }

            if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
            {
                return word[..^1];
            }

            return word;
        }

        // Optimal-string-alignment distance (counts a swap of two letters as one edit).
        // Stops early once every path exceeds maxDistance.
        internal static int EditDistance(string a, string b, int maxDistance)
        {
            if (Math.Abs(a.Length - b.Length) > maxDistance)
            {
                return maxDistance + 1;
            }

            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                int rowMin = int.MaxValue;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        v = Math.Min(v, d[i - 2, j - 2] + 1);
                    }

                    d[i, j] = v;
                    rowMin = Math.Min(rowMin, v);
                }

                if (rowMin > maxDistance)
                {
                    return maxDistance + 1;
                }
            }

            return d[a.Length, b.Length];
        }

        /// <summary>
        /// True when every word of the query is a category, subcategory or tag word (or an
        /// equivalent of one): "vpn", "gift cards", "casino". Such searches browse a kind of
        /// listing, so quality — not the wording of a name — should decide the order.
        /// </summary>
        internal static bool IsGenericQuery(ParsedQuery q, SearchIndex index) =>
            q.Words.Count > 0 && q.Words.All(w =>
                index.TaxonomyWords.Contains(w) || index.TaxonomyWords.Contains(Stem(w))
                || (SearchSynonyms.Equivalents.TryGetValue(w, out var eq) && eq.Any(index.TaxonomyWords.Contains)));

        // "e sim" → "esim" (a lone letter joined to the next word when that is a known word);
        // an unknown squashed word split into two words that appear together in a subcategory or
        // category name ("simcard" → "sim card"; "smspool" stays as typed).
        private static ParsedQuery RewriteWords(ParsedQuery q, SearchIndex index)
        {
            if (q.Words.Count == 0)
            {
                return q;
            }

            var words = new List<string>();
            bool changed = false;
            for (int i = 0; i < q.Words.Count; i++)
            {
                var w = q.Words[i];
                if (w.Length == 1 && i + 1 < q.Words.Count && index.KnownWords.Contains(w + q.Words[i + 1]))
                {
                    words.Add(w + q.Words[i + 1]);
                    i++;
                    changed = true;
                    continue;
                }

                if (w.Length >= 6 && !index.KnownWords.Contains(w) && !index.Docs.Any(d => d.ScoreWord(w, false).Score > 0))
                {
                    var split = Enumerable.Range(3, w.Length - 5)
                        .Select(k => (A: w[..k], B: w[k..]))
                        .FirstOrDefault(p => index.TaxonomyPairs.Contains(Stem(p.A) + " " + Stem(p.B)));
                    if (split.A != null)
                    {
                        words.Add(split.A);
                        words.Add(split.B);
                        changed = true;
                        continue;
                    }
                }

                words.Add(w);
            }

            return changed ? q.WithWords(words.Distinct().ToList()) : q;
        }

        private static List<RankedResult> RankDocs(SearchIndex index, ParsedQuery q, bool relaxShortWords, bool generic, bool useRelated = false, bool showScams = false)
        {
            var docs = index.Docs;
            List<Hit> hits;
            if (q.Words.Count > 0)
            {
                var scored = docs.Select(d => (Doc: d, Result: ScoreWords(d, q, relaxShortWords, useRelated))).ToList();

                // Every word must match; otherwise fall back to "most words matched".
                hits = scored
                    .Where(x => x.Result.Matched == q.Words.Count)
                    .Select(x => new Hit(x.Doc, ToTier(x.Result.Worst, x.Result.WorstNonName, generic), x.Result.Matched, x.Result.Relevance, 0))
                    .ToList();

                if (hits.Count == 0 && q.Words.Count > 1 && scored.Max(x => x.Result.Matched) > 0)
                {
                    hits = scored
                        .Where(x => x.Result.Matched > 0)
                        .Select(x => new Hit(x.Doc, SearchMatchTier.Partial, x.Result.Matched, x.Result.Relevance, 0))
                        .ToList();
                }
            }
            else
            {
                // Only a country, "no kyc" or a domain: start from every listing; the
                // filters below decide what stays.
                hits = docs.Select(d => new Hit(d, SearchMatchTier.Taxonomy, 0, 0, 0)).ToList();
            }

            if (generic && hits.Count > 0)
            {
                var wanted = q.Words.Select(Stem).OrderBy(w => w, StringComparer.Ordinal).ToArray();
                hits = hits
                    .Select(h => h.Doc.SubcategoryParts.Any(p => p.SequenceEqual(wanted)) ? h with { Bonus = h.Bonus + ExactSubcategoryBonus } : h)
                    .ToList();
            }

            // Country narrows the results when any match relates to it; otherwise it's ignored.
            if (q.CountryCode != null)
            {
                var countryTerms = q.CountryPhrase is { Length: > 0 } p && p != "us"
                    ? q.CountryTerms.Append(p).Distinct().ToList()
                    : q.CountryTerms.ToList();
                var inCountry = hits.Where(h => h.Doc.RelatesToCountry(q.CountryCode, countryTerms)).ToList();
                if (inCountry.Count > 0 || q.Words.Count == 0)
                {
                    hits = inCountry
                        .Select(h => h with
                        {
                            Bonus = h.Bonus
                                + (h.Doc.HasCountryCode(q.CountryCode) ? CountryBonus : 0)
                                + (q.Words.Count == 0 ? CountryNameScore(h.Doc, q) : 0),
                        })
                        .ToList();
                }
            }

            // "No KYC" narrows to listings whose KYC policy says so (if there are any).
            if (q.NoKyc)
            {
                var noKyc = hits.Where(h => h.Doc.IsNoKyc).ToList();
                if (noKyc.Count > 0 || q.Words.Count == 0)
                {
                    hits = noKyc
                        .Select(h => h with { Bonus = h.Bonus + (h.Doc.Entry.KycPolicy == KycPolicy.GuaranteedNoKyc ? NoKycBonus : NoKycBonus / 2) })
                        .ToList();
                }
            }

            return Order(hits, q, index, namedByDomain: false, showScams);
        }

        // Applies the keys described in SearchRankingModel.
        private static List<RankedResult> Order(IEnumerable<Hit> hits, ParsedQuery q, SearchIndex index, bool namedByDomain, bool showScams = false)
        {
            bool generic = IsGenericQuery(q, index);
            return hits
                .Select(h =>
                {
                    int protection = Protection(h.Doc, q, generic);

                    // Named clearly enough to show a Scam listing at all ("capo" → El Capo), and
                    // to keep a Questionable/Scam one in place instead of sinking it ("0trace").
                    bool named = namedByDomain || protection >= 2 || NamesListing(h.Doc, q, generic);
                    bool namedExactly = namedByDomain || protection >= 2
                        || (!generic && q.Compact.Length >= 4 && h.Doc.NameCompact.StartsWith(q.Compact, StringComparison.Ordinal));
                    var tier = protection >= 2 ? SearchMatchTier.ExactName
                        : protection == 1 ? SearchMatchTier.NamePrefix
                        : h.Tier;
                    return new
                    {
                        h.Doc,
                        Named = named,
                        Result = new RankedResult
                        {
                            Entry = h.Doc.Entry,
                            Tier = tier,
                            Sunk = (!namedExactly || showScams) && SinkTier(h.Doc.Entry.DirectoryStatus) > 0,
                            Protection = protection,
                            MatchedWords = h.Matched,
                            Relevance = h.Relevance,
                            Bonus = h.Bonus,
                            Quality = index.Quality.TryGetValue(h.Doc.Entry.DirectoryEntryId, out var ql) ? ql : SearchRankingModel.Compute(h.Doc.Entry, SearchSignals.None),
                        },
                    };
                })

                // Scam listings only when the search names them (or for admin lookups).
                .Where(x => x.Doc.Entry.DirectoryStatus != DirectoryStatus.Scam || x.Named || showScams || index.AlwaysShowScams)
                .OrderBy(x => x.Result.Sunk ? 1 : 0)
                .ThenByDescending(x => x.Result.Protection)
                .ThenByDescending(x => x.Result.MatchedWords)
                .ThenByDescending(x => Math.Round(x.Result.Score, 6))
                .ThenBy(x => StatusOrder(x.Doc.Entry.DirectoryStatus))
                .ThenBy(x => x.Doc.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Doc.Entry.DirectoryEntryId)
                .ThenBy(x => x.Doc.Entry.ShownViaAdditionalSubcategoryId ?? 0)
                .GroupBy(x => x.Doc.Entry.DirectoryEntryId) // one result per listing, ranked by its best version
                .Select(g => PickVersion(g.Select(x => (x.Doc, x.Result)).ToList(), q))
                .ToList();
        }

        // 3 = exact name, 2 = exact name without its domain ending ("solvocard" = SolvoCard.com),
        // 1 = name starts with the search (not for generic searches), 0 = neither.
        private static int Protection(Doc d, ParsedQuery q, bool generic)
        {
            if (q.Normalized.Length == 0)
            {
                return 0;
            }

            if (d.NameLower == q.Normalized || (q.Compact.Length > 0 && d.NameCompact == q.Compact))
            {
                return 3;
            }

            if (q.Compact.Length > 0 && d.NameBaseCompact == q.Compact)
            {
                return 2;
            }

            if (!generic && q.Normalized.Length >= 3
                && (d.NameLower.StartsWith(q.Normalized, StringComparison.Ordinal)
                    || (q.Compact.Length >= 4 && d.NameCompact.StartsWith(q.Compact, StringComparison.Ordinal))))
            {
                return 1;
            }

            return 0;
        }

        // Whether the search clearly names this listing (so a Scam/Questionable one is still shown).
        // ("0trac" → 0trace, "capo" → El Capo), but not a short or generic word ("dex", "sms").
        private static bool NamesListing(Doc d, ParsedQuery q, bool generic) =>
            q.Compact.Length >= 4 && !generic
            && (d.NameCompact.StartsWith(q.Compact, StringComparison.Ordinal)
                || (q.Words.Count > 0 && q.Words.All(w => d.NameWords.Contains(w))));

        // Shows the main listing whenever the search names it ("privacygateway"), even if an
        // additional subcategory happens to score a bit higher (its name repeats the listing's
        // name). An additional subcategory's wording is shown only when the search is about it
        // ("gift cards") and the main listing's name doesn't match.
        private static RankedResult PickVersion(List<(Doc Doc, RankedResult Result)> versions, ParsedQuery q)
        {
            var best = versions[0];
            if (best.Doc.Entry.ShownViaAdditionalSubcategoryId == null)
            {
                return best.Result;
            }

            // ...but not when the main listing only matched more weakly ("proxy": ProxyStore's
            // Proxies subcategory, not its Electronics main listing).
            var main = versions.FirstOrDefault(v => v.Doc.Entry.ShownViaAdditionalSubcategoryId == null);
            return main.Doc != null && main.Doc.NameMatchesAll(q.Words) && main.Result.Tier >= best.Result.Tier
                ? new RankedResult
                {
                    Entry = main.Doc.Entry,
                    Tier = best.Result.Tier,
                    Sunk = best.Result.Sunk,
                    Protection = best.Result.Protection,
                    MatchedWords = best.Result.MatchedWords,
                    Relevance = best.Result.Relevance,
                    Bonus = best.Result.Bonus,
                    Quality = best.Result.Quality,
                }
                : best.Result;
        }

        /// <summary>
        /// Each listing plus one display copy per additional subcategory (with that subcategory's
        /// name/links/description), so a search can match and show the alternative's wording.
        /// </summary>
        private static IEnumerable<DirectoryEntry> Expand(IEnumerable<DirectoryEntry> entries)
        {
            foreach (var e in entries)
            {
                yield return e;
                foreach (var a in e.AdditionalSubcategories ?? Enumerable.Empty<DirectoryEntryAdditionalSubcategory>())
                {
                    yield return a.ApplyTo(e);
                }
            }
        }

        private static double CountryNameScore(Doc d, ParsedQuery q)
        {
            var terms = q.CountryPhrase != null ? q.CountryTerms.Append(q.CountryPhrase) : q.CountryTerms;
            return terms.Select(t => d.ScoreWord(t, relaxShortWords: false).Score).DefaultIfEmpty(0).Max() / NameWeight * SearchRankingModel.MaxRelevance;
        }

        private static bool TryCountry(string phrase, out string code)
        {
            code = string.Empty;

            // Two-letter codes are too ambiguous ("ai" is Anguilla, "no" is Norway);
            // "us" and "uk" are the common exceptions.
            if (phrase.Length < 3)
            {
                return phrase is "us" or "uk" && CountryHelper.TryGetExactCountryCode(phrase, out code);
            }

            return CountryHelper.TryGetExactCountryCode(phrase, out code);
        }

        // A generic search ("vpn") ranks by what the listing is (its subcategory/category), not by
        // whether its name happens to contain the word; a specific search ranks name matches first.
        private static SearchMatchTier ToTier(FieldClass worst, FieldClass worstNonName, bool generic) => (generic ? worstNonName : worst) switch
        {
            FieldClass.NameWhole => SearchMatchTier.NameWord,
            FieldClass.NameInside => SearchMatchTier.NameContains,
            FieldClass.Taxonomy => SearchMatchTier.Taxonomy,
            FieldClass.Category => SearchMatchTier.Category,
            _ => SearchMatchTier.Text,
        };

        // Matched words, relevance points (0..MaxRelevance) and the weakest field class any word
        // needed (the listing's tier is only as good as its weakest word).
        private static (int Matched, double Relevance, FieldClass Worst, FieldClass WorstNonName) ScoreWords(Doc d, ParsedQuery q, bool relaxShortWords, bool useRelated)
        {
            int matched = 0;
            double total = 0;
            var worst = FieldClass.NameWhole;
            var worstNonName = FieldClass.NameWhole;
            foreach (var word in q.Words)
            {
                var (s, cls, nonName) = d.ScoreWord(word, relaxShortWords);

                // Equivalents count even when the word itself matched: "Casino Royale" in the
                // Gambling subcategory is a casino by name AND a gambling listing by category.
                if (SearchSynonyms.Equivalents.TryGetValue(word, out var equivalents))
                {
                    foreach (var alt in equivalents)
                    {
                        var r = d.ScoreWord(alt, relaxShortWords);
                        if (r.Score > 0)
                        {
                            s = Math.Max(s, r.Score * EquivalentWeight);
                            cls = r.Class > cls ? r.Class : cls;
                            nonName = r.NonNameClass > nonName ? r.NonNameClass : nonName;
                        }
                    }
                }

                if (useRelated && s <= 0 && SearchSynonyms.Related.TryGetValue(word, out var related))
                {
                    foreach (var alt in related)
                    {
                        var r = d.ScoreWord(alt, relaxShortWords);
                        if (r.Score * RelatedWeight > s)
                        {
                            (s, cls, nonName) = (r.Score * RelatedWeight, r.Class, r.NonNameClass);
                        }
                    }
                }

                if (s > 0)
                {
                    matched++;
                    total += Math.Min(s, NameWeight) / NameWeight;
                    worst = cls < worst ? cls : worst;
                    worstNonName = nonName < worstNonName ? nonName : worstNonName;
                }
            }

            double relevance = q.Words.Count > 0 ? SearchRankingModel.MaxRelevance * total / q.Words.Count : 0;
            return matched > 0 ? (matched, relevance, worst, worstNonName) : (0, 0, FieldClass.None, FieldClass.None);
        }

        private static int SinkTier(DirectoryStatus status) => status switch
        {
            DirectoryStatus.Questionable => 1,
            DirectoryStatus.Scam => 2,
            _ => 0,
        };

        // True when the words are the same length and differ only by changed letters (not a
        // swap of two neighbouring letters).
        private static bool IsSubstitutionOnly(string typed, string suggested)
        {
            if (typed.Length != suggested.Length)
            {
                return false;
            }

            var diffs = Enumerable.Range(0, typed.Length).Where(i => typed[i] != suggested[i]).ToList();
            bool isSwap = diffs.Count == 2 && diffs[1] == diffs[0] + 1
                && typed[diffs[0]] == suggested[diffs[1]] && typed[diffs[1]] == suggested[diffs[0]];
            return !isSwap;
        }

        // A listing's squashed name, plus that name without a leading/trailing stop word
        // ("xmrbazaar" → "bazaar", "monerodice" → "dice"), each at least 4 letters.
        private static IEnumerable<string> NameVariants(Doc d)
        {
            yield return d.NameCompact;
            foreach (var stop in StopWords.Where(w => w.Length >= 3))
            {
                if (d.NameCompact.Length - stop.Length >= 4)
                {
                    if (d.NameCompact.StartsWith(stop, StringComparison.Ordinal))
                    {
                        yield return d.NameCompact[stop.Length..];
                    }

                    if (d.NameCompact.EndsWith(stop, StringComparison.Ordinal))
                    {
                        yield return d.NameCompact[..^stop.Length];
                    }
                }
            }
        }

        private static int StatusOrder(DirectoryStatus status) => status switch
        {
            DirectoryStatus.Verified => 0,
            DirectoryStatus.Admitted => 1,
            DirectoryStatus.Questionable => 2,
            DirectoryStatus.Scam => 3,
            _ => 4,
        };

        private static string Compact(string s) => new (s.Where(char.IsLetterOrDigit).ToArray());

        private static string[] Words(string? text) =>
            string.IsNullOrEmpty(text)
                ? Array.Empty<string>()
                : WordSplit.Split(Fold(text.ToLowerInvariant())).Where(w => w.Length > 0).ToArray();

        private static string? ExtractHost(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var s = url.Trim().ToLowerInvariant();
            if (!s.Contains("://", StringComparison.Ordinal))
            {
                s = "https://" + s;
            }

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            var host = uri.Host;
            return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        }

        private sealed record Hit(Doc Doc, SearchMatchTier Tier, int Matched, double Relevance, double Bonus);

        /// <summary>One listing's searchable text, split into words once.</summary>
        internal sealed class Doc
        {
            private readonly string[] descriptionWords;
            private readonly string[] noteWords;
            private readonly string[] processorWords;
            private readonly string[] locationWords;
            private readonly string textLower;
            private readonly List<string> hosts;

            public Doc(DirectoryEntry e)
            {
                this.Entry = e;
                this.NameLower = Fold((e.Name ?? string.Empty).Trim().ToLowerInvariant());
                this.NameCompact = Compact(this.NameLower);

                // A name that is a domain ("SolvoCard.com", "COIN.HOST") is also matched without its ending.
                // Only common generic endings: "sms.lease" or "VPN.CD" must not become "sms"/"vpn".
                int dot = this.NameLower.LastIndexOf('.');
                this.NameBaseCompact = !this.NameLower.Contains(' ') && dot > 0 && DomainLike.IsMatch(this.NameLower)
                    && CommonNameEndings.Contains(this.NameLower[(dot + 1)..])
                    ? Compact(this.NameLower[..dot])
                    : this.NameCompact;
                this.NameWords = Words(e.Name);

                var tagNames = (e.EntryTags ?? new List<DirectoryEntryTag>())
                    .Select(et => et.Tag?.Name)
                    .Append(e.Tags);
                this.TagWords = tagNames.SelectMany(Words).ToArray();

                this.SubcategoryWords = Words(e.SubCategory?.Name);

                // "eSIMs & SIM Cards" → [esim], [card, sim] (stemmed, sorted), ignoring "(No Account)".
                this.SubcategoryParts = Regex.Replace(e.SubCategory?.Name ?? string.Empty, @"\([^)]*\)", " ")
                    .Split('&', '/', ',')
                    .Select(part => Words(part).Where(w => w != "and").Select(Stem).OrderBy(w => w, StringComparer.Ordinal).ToArray())
                    .Where(a => a.Length > 0)
                    .ToArray();
                this.CategoryWords = Words(e.SubCategory?.Category?.Name);
                this.CategoryPrimaryWords = Words((e.SubCategory?.Category?.Name ?? string.Empty).Split('&', '/')[0]);
                this.descriptionWords = Words(e.Description);
                this.noteWords = Words(e.Note);
                this.processorWords = Words(e.Processor);
                this.locationWords = Words(PostalAddress.Format(e.StreetAddress, e.City, e.StateRegion, e.PostalCode));

                this.hosts = new[] { e.Link, e.Link2, e.Link3, e.ProofLink }
                    .Concat(e.SearchAdditionalLinks ?? new List<string>())
                    .Select(ExtractHost)
                    .Where(h => !string.IsNullOrEmpty(h))
                    .Select(h => h!)
                    .Distinct()
                    .ToList();

                // Host labels without the TLD: "xmr.cards" → "xmr", "cards".
                this.HostLabels = this.hosts
                    .SelectMany(h =>
                    {
                        var parts = h.Split('.', StringSplitOptions.RemoveEmptyEntries);
                        return parts.Length > 1 ? parts[..^1] : parts;
                    })
                    .SelectMany(p => p.Split('-', StringSplitOptions.RemoveEmptyEntries))
                    .Distinct()
                    .ToArray();

                this.textLower = Fold(string.Join(' ', new[] { e.Description, PostalAddress.Format(e.StreetAddress, e.City, e.StateRegion, e.PostalCode), e.Note }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant());
            }

            public DirectoryEntry Entry { get; }

            public string NameLower { get; }

            public string NameCompact { get; }

            public string NameBaseCompact { get; }

            public string[] NameWords { get; }

            public string[] TagWords { get; }

            public string[] SubcategoryWords { get; }

            public string[][] SubcategoryParts { get; }

            public string[] CategoryWords { get; }

            public string[] CategoryPrimaryWords { get; }

            public string[] HostLabels { get; }

            public bool IsNoKyc => this.Entry.KycPolicy is KycPolicy.GuaranteedNoKyc or KycPolicy.RareKyc;

            public bool NameMatchesAll(IReadOnlyList<string> words) =>
                words.Count > 0 && words.All(w =>
                    Match(this.NameWords, w, allowInside: true) > 0
                    || (w.Length > ShortWordMaxLength && this.NameCompact.Contains(w, StringComparison.Ordinal)));

            public bool HasCountryCode(string code) =>
                string.Equals(this.Entry.CountryCode, code, StringComparison.OrdinalIgnoreCase);

            public bool RelatesToCountry(string code, IReadOnlyList<string> terms)
            {
                if (this.HasCountryCode(code))
                {
                    return true;
                }

                foreach (var t in terms)
                {
                    var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(t)}(?![\p{{L}}\p{{N}}])";
                    if (Regex.IsMatch(this.textLower, pattern) || Regex.IsMatch(this.NameLower, pattern))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>1 when a link is on exactly this domain (or a subdomain of it), 0.5 when it merely contains it.</summary>
            public double HostMatch(string host)
            {
                double best = 0;
                foreach (var h in this.hosts)
                {
                    if (h == host || h.EndsWith("." + host, StringComparison.Ordinal))
                    {
                        return 1;
                    }

                    if (h.Contains(host, StringComparison.Ordinal))
                    {
                        best = 0.5;
                    }
                }

                return best;
            }

            /// <summary>
            /// Best field match for one query word, plus a little credit for matching other
            /// fields too, and the class of the best field (name, category/tag, or text).
            /// 0 means the word appears nowhere in this listing. Each field counts once, however
            /// often the word is repeated in it.
            /// </summary>
            public (double Score, FieldClass Class, FieldClass NonNameClass) ScoreWord(string word, bool relaxShortWords)
            {
                double name = Match(this.NameWords, word, allowInside: true);

                // Fallback for short words that matched nothing anywhere: name words starting with it.
                if (relaxShortWords && word.Length <= ShortWordMaxLength && name == 0
                    && this.NameWords.Any(w => w.StartsWith(word, StringComparison.Ordinal)))
                {
                    name = PrefixMatch;
                }

                // Squashed names: "ghostswap" should find "GhostSwap", "xmrcards" → "XMR.CARDS".
                if (word.Length > ShortWordMaxLength && this.NameCompact.Contains(word, StringComparison.Ordinal))
                {
                    name = Math.Max(name, this.NameCompact == word ? WordMatch : InsideWordMatch);
                }

                double sub = SubcategoryWeight * Match(this.SubcategoryWords, word, allowInside: false);
                double tag = TagWeight * Match(this.TagWords, word, allowInside: false);
                double host = HostWeight * Match(this.HostLabels, word, allowInside: true);
                double cat = CategoryWeight * Match(this.CategoryWords, word, allowInside: false);
                double text = Math.Max(
                    Math.Max(DescriptionWeight * Match(this.descriptionWords, word, allowInside: false), LocationWeight * Match(this.locationWords, word, allowInside: false)),
                    Math.Max(NoteWeight * Match(this.noteWords, word, allowInside: false), ProcessorWeight * Match(this.processorWords, word, allowInside: false)));

                var scores = new[] { NameWeight * name, sub, tag, host, cat, text };
                double best = scores.Max();
                if (best <= 0)
                {
                    return (0, FieldClass.None, FieldClass.None);
                }

                // Subcategory, or the main noun of the category ("VPNs" in "VPNs & Proxies", whose
                // subcategories are jurisdictions), is what a listing IS. The rest of a category
                // name, the domain and the feature tags ("Email Not Required") are weaker.
                bool primaryCategory = cat > 0 && Match(this.CategoryPrimaryWords, word, allowInside: false) > 0;
                var cls = name >= WordMatch ? FieldClass.NameWhole
                    : name > 0 ? FieldClass.NameInside
                    : sub > 0 || primaryCategory ? FieldClass.Taxonomy
                    : host > 0 || cat > 0 || tag > 0 ? FieldClass.Category
                    : FieldClass.Text;

                // What the listing IS, ignoring its name (for generic searches: "ProxyStore", an
                // electronics shop, is only a weak match for "proxy").
                var nonName = sub > 0 || primaryCategory ? FieldClass.Taxonomy
                    : host > 0 || cat > 0 || tag > 0 || name > 0 ? FieldClass.Category
                    : FieldClass.Text;

                return (best + (0.2 * (scores.Sum() - best)), cls, nonName);
            }

            private static double Match(string[] fieldWords, string word, bool allowInside)
            {
                if (fieldWords.Length == 0)
                {
                    return 0;
                }

                var stem = Stem(word);
                bool isShort = word.Length <= ShortWordMaxLength;
                double best = 0;
                foreach (var fw in fieldWords)
                {
                    if (fw == word || Stem(fw) == stem)
                    {
                        return WordMatch;
                    }

                    if (isShort)
                    {
                        continue;
                    }

                    if (fw.StartsWith(stem, StringComparison.Ordinal))
                    {
                        best = Math.Max(best, PrefixMatch);
                    }
                    else if (allowInside && fw.Contains(stem, StringComparison.Ordinal))
                    {
                        best = Math.Max(best, InsideWordMatch);
                    }
                }

                return best;
            }
        }
    }
}
