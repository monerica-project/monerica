namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Stops one term from flooding the search log. A script that repeats the same search hundreds
    /// of times in an hour (4,401 "test" on 2026-05-02, 246 "sim" on 2025-11-14) is answered every
    /// time but logged only a few times. Counts by term alone, in memory: no visitor data is used.
    /// </summary>
    public static class SearchLogRepeatGuard
    {
        public const int MaxPerTermPerHour = 5;
        private const int MaxTrackedTerms = 5000;
        private static readonly object Gate = new ();
        private static readonly Dictionary<string, (DateTime WindowStart, int Count)> Seen = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>True when this term may still be logged in the current hour.</summary>
        public static bool Allow(string? term, DateTime utcNow)
        {
            var key = (term ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return false;
            }

            lock (Gate)
            {
                if (Seen.Count > MaxTrackedTerms)
                {
                    foreach (var old in Seen.Where(kv => utcNow - kv.Value.WindowStart >= TimeSpan.FromHours(1)).Select(kv => kv.Key).ToList())
                    {
                        Seen.Remove(old);
                    }

                    if (Seen.Count > MaxTrackedTerms)
                    {
                        Seen.Clear();
                    }
                }

                if (!Seen.TryGetValue(key, out var e) || utcNow - e.WindowStart >= TimeSpan.FromHours(1))
                {
                    Seen[key] = (utcNow, 1);
                    return true;
                }

                if (e.Count >= MaxPerTermPerHour)
                {
                    return false;
                }

                Seen[key] = (e.WindowStart, e.Count + 1);
                return true;
            }
        }

        /// <summary>Forgets everything (tests only).</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                Seen.Clear();
            }
        }
    }
}
