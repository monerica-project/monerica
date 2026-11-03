using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Logs that someone looked at sponsoring a listing (for the admin sponsor-interest report).
    /// No IP or visitor identity is stored. Admins (signed in) and obvious bots are skipped, and
    /// the same listing / placement / step is counted once per <see cref="DedupeWindow"/>.
    /// Never throws: a logging failure must not break checkout.
    /// </summary>
    public static class SponsorInterestTracker
    {
        public static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(30);

        private static readonly string[] BotMarkers =
        {
            "bot", "crawl", "spider", "slurp", "curl", "wget", "python", "httpclient", "headless", "preview", "scan",
        };

        public static bool IsBot(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return true;
            }

            var ua = userAgent.ToLowerInvariant();
            return BotMarkers.Any(ua.Contains);
        }

        public static async Task LogAsync(HttpContext http, int directoryEntryId, SponsorshipType type, int? typeId, SponsorInterestStep step)
        {
            try
            {
                if (directoryEntryId <= 0
                    || http.User?.Identity?.IsAuthenticated == true
                    || IsBot(http.Request.Headers.UserAgent.ToString()))
                {
                    return;
                }

                var repo = http.RequestServices.GetService<ISponsorInterestLogRepository>();
                if (repo == null)
                {
                    return;
                }

                int? scope = type is SponsorshipType.CategorySponsor or SponsorshipType.SubcategorySponsor && typeId > 0 ? typeId : null;
                await repo.LogAsync(directoryEntryId, type, scope, step, DedupeWindow).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[sponsor-interest] log failed: {ex.Message}");
            }
        }
    }
}
