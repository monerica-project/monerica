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
    /// 1. Words, not phrases: every meaningful word must match somewhere, in any order.
    ///    If nothing matches every word, listings matching the most words are shown.
    /// 2. Relevance first: a name match beats a tag/category match, which beats a passing
    ///    mention in the description. Listing status only nudges the order; Questionable
    ///    and Scam listings sink.
    /// 3. Words of 3 letters or fewer ("ai", "rdp", "tor") only match whole words, so "ai"
    ///    no longer matches "mail".
    /// 4. A country word ("esim usa") narrows the other words' results to that country
    ///    instead of returning every listing in the country.
    /// 5. <see cref="SuggestCorrection"/> offers a spelling fix ("trocadr" → "trocador")
    ///    when a search finds nothing.
    /// </summary>
    public static class DirectorySearchEngine
    {
        // Field weights: how much a full-word match in each field is worth.
        private const double NameWeight = 100;
        private const double SubcategoryWeight = 45;
        private const double TagWeight = 40;
        private const double HostWeight = 35;
        private const double CategoryWeight = 30;
        private const double DescriptionWeight = 15;
        private const double LocationWeight = 10;
        private const double NoteWeight = 8;
        private const double ProcessorWeight = 8;

        // Whole-name bonuses for the full query (e.g. searching a listing's exact name).
        private const double ExactNameBonus = 300;
        private const double NameStartsWithBonus = 120;
        private const double ExactHostBonus = 250;

        private const double CountryBonus = 40;
        private const double NoKycBonus = 25;

        // Match strengths within a field.
        private const double WordMatch = 1.0;
        private const double PrefixMatch = 0.7;
        private const double InsideWordMatch = 0.5;

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

        // Dropped when the query has other words: they don't narrow anything in a
        // Monero-only directory ("buy esim", "monero vps").
        private static readonly HashSet<string> StopWords = new (StringComparer.Ordinal)
        {
            "a", "an", "the", "and", "or", "for", "with", "to", "in", "on", "of", "by", "at", "from",
            "buy", "buying", "pay", "paying", "use", "using", "accept", "accepts", "accepting",
            "get", "find", "best", "cheap", "site", "sites", "website", "websites", "service", "services",
            "monero", "xmr", "crypto", "cryptocurrency", "cryptocurrencies",
        };

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

        /// <summary>Returns the matching listings, best match first.</summary>
        public static List<DirectoryEntry> Rank(IEnumerable<DirectoryEntry> entries, string query)
        {
            var q = Parse(query);
            if (q.Words.Count == 0 && q.CountryCode == null && !q.NoKyc && q.Host == null)
            {
                return new List<DirectoryEntry>();
            }

            var docs = entries.Select(e => new Doc(e)).ToList();

            // Domain search ("xmr.cards"): if a listing is on that domain, show just those.
            if (q.Host != null)
            {
                var hostDocs = docs.Where(d => d.HostMatch(q.Host) > 0).ToList();
                if (hostDocs.Count > 0)
                {
                    return Order(hostDocs.Select(d => (d, d.HostMatch(q.Host) * ExactHostBonus)), q);
                }
            }

            var ranked = RankDocs(docs, q, relaxShortWords: false);

            // A short word that matched nothing as a whole word ("bit") is probably the start of
            // a name: retry letting it match the beginning of listing names (Bitania, BitcoinVN).
            if (ranked.Count == 0 && q.Words.Any(w => w.Length <= ShortWordMaxLength))
            {
                ranked = RankDocs(docs, q, relaxShortWords: true);
            }

            return ranked;
        }

        private static List<DirectoryEntry> RankDocs(List<Doc> docs, ParsedQuery q, bool relaxShortWords)
        {
            var scored = docs
                .Select(d => (Doc: d, Result: ScoreWords(d, q, relaxShortWords)))
                .ToList();

            List<(Doc Doc, double Score)> hits;
            if (q.Words.Count > 0)
            {
                // Every word must match; otherwise fall back to "most words matched".
                hits = scored
                    .Where(x => x.Result.Matched == q.Words.Count)
                    .Select(x => (x.Doc, x.Result.Score))
                    .ToList();

                if (hits.Count == 0 && q.Words.Count > 1)
                {
                    int best = scored.Max(x => x.Result.Matched);
                    if (best > 0)
                    {
                        hits = scored
                            .Where(x => x.Result.Matched > 0)
                            .Select(x => (x.Doc, (x.Result.Matched * 1000) + x.Result.Score))
                            .ToList();
                    }
                }
            }
            else
            {
                // Only a country, "no kyc" or a domain: start from every listing; the
                // filters below decide what stays.
                hits = docs.Select(d => (d, 0d)).ToList();
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
                        .Select(h => (h.Doc, h.Score
                            + (h.Doc.HasCountryCode(q.CountryCode) ? CountryBonus : 0)
                            + (q.Words.Count == 0 ? CountryNameScore(h.Doc, q) : 0)))
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
                        .Select(h => (h.Doc, h.Score + (h.Doc.Entry.KycPolicy == KycPolicy.GuaranteedNoKyc ? NoKycBonus : NoKycBonus / 2)))
                        .ToList();
                }
            }

            return Order(hits, q);
        }

        // Best match first. Questionable and Scam listings go after the rest, unless the
        // search was for that listing's exact name (so people can still look one up).
        private static List<DirectoryEntry> Order(IEnumerable<(Doc Doc, double Score)> hits, ParsedQuery q)
        {
            return hits
                .Select(h =>
                {
                    double whole = WholeQueryBonus(h.Doc, q);
                    int sink = whole >= ExactNameBonus ? 0 : SinkTier(h.Doc.Entry.DirectoryStatus);
                    return (h.Doc, Sink: sink, Score: h.Score + whole + StatusAdjustment(h.Doc.Entry.DirectoryStatus));
                })
                .OrderBy(h => h.Sink)
                .ThenByDescending(h => h.Score)
                .ThenBy(h => StatusOrder(h.Doc.Entry.DirectoryStatus))
                .ThenBy(h => h.Doc.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(h => h.Doc.Entry)
                .ToList();
        }

        private static double CountryNameScore(Doc d, ParsedQuery q)
        {
            var terms = q.CountryPhrase != null ? q.CountryTerms.Append(q.CountryPhrase) : q.CountryTerms;
            return terms.Select(t => d.ScoreWord(t, relaxShortWords: false)).DefaultIfEmpty(0).Max();
        }

        /// <summary>
        /// When a search finds nothing, returns the query with misspelled words replaced by the
        /// closest word used in listing names, tags or categories ("trocadr" → "trocador"), or
        /// null if there's no close match. Scam and Questionable listings are never suggested.
        /// </summary>
        public static Correction? SuggestCorrection(IEnumerable<DirectoryEntry> entries, string query)
        {
            var q = Parse(query);
            if (q.Words.Count == 0)
            {
                return null;
            }

            var docs = entries.Select(e => new Doc(e)).ToList();

            // Vocabulary: words that identify listings, weighted by how often they appear.
            var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var d in docs.Where(d => SinkTier(d.Entry.DirectoryStatus) == 0))
            {
                foreach (var w in d.NameWords.Concat(d.TagWords).Concat(d.SubcategoryWords).Concat(d.CategoryWords).Concat(d.HostLabels))
                {
                    if (w.Length >= 3)
                    {
                        vocab[w] = vocab.GetValueOrDefault(w) + 1;
                    }
                }
            }

            bool changed = false;
            bool autoApply = true;
            var corrected = q.Normalized;
            foreach (var word in q.Words)
            {
                bool known = word.Length < 4 || docs.Any(d => d.ScoreWord(word, relaxShortWords: false) > 0);
                if (known)
                {
                    continue;
                }

                int maxDistance = word.Length <= 5 ? 1 : 2;
                var best = vocab
                    .Select(kv => (Word: kv.Key, Count: kv.Value, Distance: EditDistance(word, kv.Key, maxDistance)))
                    .Where(x => x.Distance <= maxDistance)
                    .OrderBy(x => x.Distance)
                    .ThenByDescending(x => x.Count)
                    .ThenBy(x => x.Word, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (best.Word != null)
                {
                    corrected = Regex.Replace(
                        corrected,
                        $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])",
                        best.Word);
                    changed = true;
                    autoApply &= !IsSubstitutionOnly(word, best.Word);
                }
            }

            return changed && corrected != q.Normalized ? new Correction(corrected, autoApply) : null;
        }

        public static ParsedQuery Parse(string? query)
        {
            var normalized = Regex.Replace((query ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");

            string? host = null;
            if (!normalized.Contains(' ') && DomainLike.IsMatch(normalized))
            {
                host = ExtractHost(normalized);
            }

            bool noKyc = NoKycRegex.IsMatch(normalized);
            var rest = noKyc ? NoKycRegex.Replace(normalized, " ") : normalized;

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

        private static (int Matched, double Score) ScoreWords(Doc d, ParsedQuery q, bool relaxShortWords)
        {
            int matched = 0;
            double total = 0;
            foreach (var word in q.Words)
            {
                double s = d.ScoreWord(word, relaxShortWords);
                if (s > 0)
                {
                    matched++;
                    total += s;
                }
            }

            return (matched, total);
        }

        private static double WholeQueryBonus(Doc d, ParsedQuery q)
        {
            if (q.Normalized.Length == 0)
            {
                return 0;
            }

            if (d.NameLower == q.Normalized || (q.Compact.Length > 0 && d.NameCompact == q.Compact))
            {
                return ExactNameBonus;
            }

            if (q.Normalized.Length >= 3 && d.NameLower.StartsWith(q.Normalized, StringComparison.Ordinal))
            {
                return NameStartsWithBonus;
            }

            return 0;
        }

        private static double StatusAdjustment(DirectoryStatus status) => status switch
        {
            DirectoryStatus.Verified => 10,
            DirectoryStatus.Questionable => -30,
            DirectoryStatus.Scam => -60,
            _ => 0,
        };

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
                : WordSplit.Split(text.ToLowerInvariant()).Where(w => w.Length > 0).ToArray();

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

        /// <summary>One listing's searchable text, split into words once.</summary>
        private sealed class Doc
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
                this.NameLower = (e.Name ?? string.Empty).Trim().ToLowerInvariant();
                this.NameCompact = Compact(this.NameLower);
                this.NameWords = Words(e.Name);

                var tagNames = (e.EntryTags ?? new List<DirectoryEntryTag>())
                    .Select(et => et.Tag?.Name)
                    .Append(e.Tags);
                this.TagWords = tagNames.SelectMany(Words).ToArray();

                this.SubcategoryWords = Words(e.SubCategory?.Name);
                this.CategoryWords = Words(e.SubCategory?.Category?.Name);
                this.descriptionWords = Words(e.Description);
                this.noteWords = Words(e.Note);
                this.processorWords = Words(e.Processor);
                this.locationWords = Words(e.Location);

                this.hosts = new[] { e.Link, e.Link2, e.Link3, e.ProofLink }
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

                this.textLower = string.Join(' ', new[] { e.Description, e.Location, e.Note }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();
            }

            public DirectoryEntry Entry { get; }

            public string NameLower { get; }

            public string NameCompact { get; }

            public string[] NameWords { get; }

            public string[] TagWords { get; }

            public string[] SubcategoryWords { get; }

            public string[] CategoryWords { get; }

            public string[] HostLabels { get; }

            public bool IsNoKyc => this.Entry.KycPolicy is KycPolicy.GuaranteedNoKyc or KycPolicy.RareKyc;

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
            /// fields too. 0 means the word appears nowhere in this listing.
            /// </summary>
            public double ScoreWord(string word, bool relaxShortWords)
            {
                var scores = new[]
                {
                    NameWeight * Match(this.NameWords, word, allowInside: true),
                    SubcategoryWeight * Match(this.SubcategoryWords, word, allowInside: false),
                    TagWeight * Match(this.TagWords, word, allowInside: false),
                    HostWeight * Match(this.HostLabels, word, allowInside: true),
                    CategoryWeight * Match(this.CategoryWords, word, allowInside: false),
                    DescriptionWeight * DescriptionMatch(this.descriptionWords, word),
                    LocationWeight * Match(this.locationWords, word, allowInside: false),
                    NoteWeight * Match(this.noteWords, word, allowInside: false),
                    ProcessorWeight * Match(this.processorWords, word, allowInside: false),
                };

                // Fallback for short words that matched nothing anywhere: name words starting with it.
                if (relaxShortWords && word.Length <= ShortWordMaxLength && scores[0] == 0
                    && this.NameWords.Any(w => w.StartsWith(word, StringComparison.Ordinal)))
                {
                    scores[0] = NameWeight * PrefixMatch;
                }

                // Squashed names: "ghostswap" should find "GhostSwap", "xmrcards" → "XMR.CARDS".
                if (word.Length > ShortWordMaxLength && this.NameCompact.Contains(word, StringComparison.Ordinal))
                {
                    scores[0] = Math.Max(scores[0], NameWeight * (this.NameCompact == word ? WordMatch : InsideWordMatch));
                }

                double best = scores.Max();
                if (best <= 0)
                {
                    return 0;
                }

                return best + (0.2 * (scores.Sum() - best));
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

            // Description: whole-word/prefix matches, plus a little for repeated mentions.
            private static double DescriptionMatch(string[] fieldWords, string word)
            {
                if (fieldWords.Length == 0)
                {
                    return 0;
                }

                var stem = Stem(word);
                bool isShort = word.Length <= ShortWordMaxLength;
                int full = 0;
                bool prefix = false;
                foreach (var fw in fieldWords)
                {
                    if (fw == word || Stem(fw) == stem)
                    {
                        full++;
                    }
                    else if (!isShort && fw.StartsWith(stem, StringComparison.Ordinal))
                    {
                        prefix = true;
                    }
                }

                if (full > 0)
                {
                    return WordMatch + (0.2 * Math.Min(full - 1, 3));
                }

                return prefix ? PrefixMatch : 0;
            }
        }
    }
}
