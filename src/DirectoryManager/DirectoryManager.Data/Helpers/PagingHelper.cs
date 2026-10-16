namespace DirectoryManager.Data.Helpers
{
    /// <summary>
    /// Defensive paging math for repository queries. Callers (controllers) should already clamp
    /// user-supplied page numbers, but the repositories never trust them: a page below 1 or a
    /// negative size must not become a negative OFFSET/LIMIT (Postgres rejects those -> 500), and
    /// a huge page must not overflow <c>(page - 1) * pageSize</c> into a negative int.
    /// </summary>
    public static class PagingHelper
    {
        /// <summary>The largest page size a public page may request.</summary>
        public const int MaxPublicPageSize = 100;

        /// <summary>Page numbers are 1-based; anything below 1 becomes 1.</summary>
        public static int ClampPage(int page) => page < 1 ? 1 : page;

        /// <summary>
        /// Clamps a requested page size into [1, <paramref name="max"/>]; values below 1 fall back
        /// to <paramref name="fallback"/> (itself clamped into the same range).
        /// </summary>
        public static int ClampPageSize(int pageSize, int fallback, int max = MaxPublicPageSize)
        {
            if (max < 1)
            {
                max = 1;
            }

            if (pageSize < 1)
            {
                pageSize = fallback;
            }

            return Math.Clamp(pageSize, 1, max);
        }

        /// <summary>Rows to skip for a 1-based page; never negative and never overflows.</summary>
        public static int Skip(int page, int pageSize)
        {
            long skip = (long)(ClampPage(page) - 1) * Take(pageSize);
            return skip > int.MaxValue ? int.MaxValue : (int)skip;
        }

        /// <summary>Rows to take; never negative.</summary>
        public static int Take(int pageSize) => pageSize < 0 ? 0 : pageSize;

        /// <summary>Total number of pages (at least 1, so page 1 of an empty list is valid).</summary>
        public static int TotalPages(int totalCount, int pageSize)
        {
            if (pageSize < 1 || totalCount <= 0)
            {
                return 1;
            }

            return (int)Math.Max(1, ((long)totalCount + pageSize - 1) / pageSize);
        }
    }
}
