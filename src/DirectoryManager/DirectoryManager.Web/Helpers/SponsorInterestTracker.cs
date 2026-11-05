using Microsoft.AspNetCore.Mvc.ViewFeatures;
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

        /// <summary>
        /// Which id to keep with the row: the category / subcategory for those placements, and the
        /// video offer for video sponsorship steps. Anything else has no scope.
        /// </summary>
        public static int? ScopeFor(SponsorshipType type, int? typeId, SponsorInterestStep step)
        {
            if (typeId is not > 0)
            {
                return null;
            }

            return step.IsVideo() || type is SponsorshipType.CategorySponsor or SponsorshipType.SubcategorySponsor ? typeId : null;
        }

        /// <summary>ViewData key the base layout reads to render the counting image.</summary>
        public const string BeaconViewDataKey = "SponsorInterestBeacon";

        /// <summary>Path of the 1x1 counting image (served by SponsorInterestBeaconController).</summary>
        public const string BeaconPath = "/sponsor-interest/seen.gif";

        /// <summary>Steps that are page views: counted only when the browser loads the beacon image.</summary>
        public static bool IsBeaconStep(SponsorInterestStep step) => step is
            SponsorInterestStep.ViewedOptions or SponsorInterestStep.ViewedPrices or SponsorInterestStep.PickedDuration or
            SponsorInterestStep.VideoViewedPrices or SponsorInterestStep.VideoPickedVideos or SponsorInterestStep.VideoReviewedOrder;

        /// <summary>
        /// Page views are counted with a 1x1 image instead of the HTML request: crawlers that fake a
        /// normal browser user agent fetch the HTML but don't load images. The layout renders the
        /// image when this is set.
        /// </summary>
        public static void SetBeacon(ViewDataDictionary viewData, int directoryEntryId, SponsorshipType type, int? typeId, SponsorInterestStep step)
        {
            if (directoryEntryId <= 0 || !IsBeaconStep(step))
            {
                return;
            }

            var scope = ScopeFor(type, typeId, step);
            viewData[BeaconViewDataKey] = $"{BeaconPath}?e={directoryEntryId}&t={(int)type}&s={(int)step}" + (scope is int sc ? $"&p={sc}" : string.Empty);
        }

        /// <summary>
        /// Browsers say what a request is for. An image load is Sec-Fetch-Dest "image" from the same site;
        /// anything that says otherwise (a crawler fetching the URL directly) is not counted. Older
        /// browsers that send no Sec-Fetch headers are allowed.
        /// </summary>
        public static bool LooksLikeImageLoad(string? fetchDest, string? fetchSite)
        {
            if (!string.IsNullOrEmpty(fetchDest) && !string.Equals(fetchDest, "image", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.IsNullOrEmpty(fetchSite) || string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase);
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

                int? scope = ScopeFor(type, typeId, step);
                await repo.LogAsync(directoryEntryId, type, scope, step, DedupeWindow).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[sponsor-interest] log failed: {ex.Message}");
            }
        }
    }
}
