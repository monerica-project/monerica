using System.Globalization;
using System.Text;

namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>
    /// The filters of the public /reviews page, parsed from raw query-string values. Anything
    /// that doesn't parse, or names a category/tag that doesn't exist, is dropped (never an
    /// error), so junk parameters just show the unfiltered page.
    /// </summary>
    public sealed class PublicReviewFilter
    {
        public const int PageSize = 20;
        public const int MaxSearchLength = 100;
        public const int MaxPage = 10000;

        public string? Search { get; init; }

        public string? CategoryKey { get; init; }

        public int? CategoryId { get; init; }

        public byte? Rating { get; init; }

        public string? TagSlug { get; init; }

        public int? TagId { get; init; }

        public bool WithReplies { get; init; }

        public PublicReviewSort Sort { get; init; } = PublicReviewSort.Newest;

        public int Page { get; init; } = 1;

        /// <summary>True when any filter (not paging) is set.</summary>
        public bool HasFilters =>
            this.Search != null || this.CategoryId != null || this.Rating != null || this.TagId != null
            || this.WithReplies || this.Sort != PublicReviewSort.Newest;

        /// <summary>The plain first page: the only variant that is indexed.</summary>
        public bool IsDefault => !this.HasFilters && this.Page == 1;

        public static PublicReviewFilter Parse(
            string? q,
            string? category,
            string? rating,
            string? tag,
            string? replies,
            string? sort,
            string? page,
            IReadOnlyDictionary<string, int>? categoryIdsByKey,
            IReadOnlyDictionary<string, int>? tagIdsBySlug)
        {
            string? categoryKey = null;
            int? categoryId = null;
            var cat = (category ?? string.Empty).Trim();
            if (cat.Length > 0 && cat.Length <= 100 && categoryIdsByKey != null
                && categoryIdsByKey.TryGetValue(cat.ToLowerInvariant(), out var cid))
            {
                categoryKey = cat.ToLowerInvariant();
                categoryId = cid;
            }

            string? tagSlug = null;
            int? tagId = null;
            var t = (tag ?? string.Empty).Trim();
            if (t.Length > 0 && t.Length <= 64 && tagIdsBySlug != null
                && tagIdsBySlug.TryGetValue(t.ToLowerInvariant(), out var tid))
            {
                tagSlug = t.ToLowerInvariant();
                tagId = tid;
            }

            byte? stars = null;
            if (int.TryParse((rating ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var r)
                && r >= 1 && r <= 5)
            {
                stars = (byte)r;
            }

            var withReplies = (replies ?? string.Empty).Trim().ToLowerInvariant() is "1" or "true" or "on" or "yes";

            var sortValue = (sort ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "oldest" => PublicReviewSort.Oldest,
                "highest" => PublicReviewSort.Highest,
                "lowest" => PublicReviewSort.Lowest,
                _ => PublicReviewSort.Newest,
            };

            return new PublicReviewFilter
            {
                Search = CleanSearch(q),
                CategoryKey = categoryKey,
                CategoryId = categoryId,
                Rating = stars,
                TagSlug = tagSlug,
                TagId = tagId,
                WithReplies = withReplies,
                Sort = sortValue,
                Page = ParsePage(page),
            };
        }

        /// <summary>1-based page from a raw value; anything invalid is 1, huge values are capped.</summary>
        public static int ParsePage(string? raw)
        {
            if (!int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) || p < 1)
            {
                return 1;
            }

            return Math.Min(p, MaxPage);
        }

        /// <summary>
        /// Trims, drops control characters, collapses whitespace and caps the length at
        /// <see cref="MaxSearchLength"/> (never splitting a surrogate pair). Empty → null.
        /// </summary>
        public static string? CleanSearch(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var sb = new StringBuilder(raw.Length);
            bool lastWasSpace = false;
            foreach (var ch in raw)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    lastWasSpace = true;
                    continue;
                }

                if (char.IsControl(ch))
                {
                    continue;
                }

                sb.Append(ch);
                lastWasSpace = false;
            }

            var s = sb.ToString().Trim();
            if (s.Length > MaxSearchLength)
            {
                int cut = MaxSearchLength;
                if (char.IsHighSurrogate(s[cut - 1]))
                {
                    cut--;
                }

                s = s.Substring(0, cut).TrimEnd();
            }

            return s.Length == 0 ? null : s;
        }

        /// <summary>A copy of this filter on another page (e.g. after clamping to the last page).</summary>
        public PublicReviewFilter WithPage(int page) => new ()
        {
            Search = this.Search,
            CategoryKey = this.CategoryKey,
            CategoryId = this.CategoryId,
            Rating = this.Rating,
            TagSlug = this.TagSlug,
            TagId = this.TagId,
            WithReplies = this.WithReplies,
            Sort = this.Sort,
            Page = page < 1 ? 1 : page,
        };

        /// <summary>
        /// The query string for this filter (with a leading "?", or empty), defaults left out,
        /// in a fixed order. <paramref name="page"/> overrides the page (1 is left out).
        /// </summary>
        public string ToQueryString(int? page = null)
        {
            var parts = new List<string>();
            if (this.Search != null)
            {
                parts.Add("q=" + Uri.EscapeDataString(this.Search));
            }

            if (this.CategoryKey != null)
            {
                parts.Add("category=" + Uri.EscapeDataString(this.CategoryKey));
            }

            if (this.Rating != null)
            {
                parts.Add("rating=" + this.Rating.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (this.TagSlug != null)
            {
                parts.Add("tag=" + Uri.EscapeDataString(this.TagSlug));
            }

            if (this.WithReplies)
            {
                parts.Add("replies=1");
            }

            if (this.Sort != PublicReviewSort.Newest)
            {
                parts.Add("sort=" + this.Sort.ToString().ToLowerInvariant());
            }

            var p = page ?? this.Page;
            if (p > 1)
            {
                parts.Add("page=" + p.ToString(CultureInfo.InvariantCulture));
            }

            return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        }
    }
}
