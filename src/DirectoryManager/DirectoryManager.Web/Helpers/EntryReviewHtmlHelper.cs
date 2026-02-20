using Microsoft.AspNetCore.Html;
using System.Net;
using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Helpers
{
    public static class EntryReviewHtmlHelper
    {
        public static IHtmlContent RenderAggregateStars(double? avg, int? count, bool starsOnly = false)
        {
            if (!avg.HasValue || !count.HasValue || count.Value <= 0)
                return HtmlString.Empty;

            const int totalStars = 5;
            double percent = Math.Clamp(avg.Value / totalStars * 100.0, 0, 100);

            if (starsOnly)
            {
                return new HtmlString($@"
<span class=""rating-wrapper"" aria-label=""{avg:0.0} out of 5 stars from {count} reviews"">
  <span class=""stars""><span class=""stars-fill"" style=""width:{percent:0.##}%""></span></span>
</span>");
            }

            return new HtmlString($@"
<span class=""rating-wrapper"" aria-label=""{avg:0.0} out of 5 stars from {count} reviews"">
  <span class=""stars""><span class=""stars-fill"" style=""width:{percent:0.##}%""></span></span>
  <span class=""rating-text""> {avg:0.0}/5 ({count})</span>
</span>");
        }

        public static IHtmlContent RenderSingleReviewStars(byte? rating)
        {
            if (!rating.HasValue || rating.Value < 1)
                return HtmlString.Empty;

            double percent = Math.Clamp(rating.Value / 5.0 * 100.0, 0, 100);

            return new HtmlString($@"
<span class=""rating-wrapper"" aria-label=""{rating}/5"">
  <span class=""stars""><span class=""stars-fill"" style=""width:{percent:0.##}%""></span></span>
</span>");
        }

        public static List<DirectoryEntryReviewComment> GetReplies(
            Dictionary<int, List<DirectoryEntryReviewComment>>? dict, int reviewId)
        {
            if (dict == null) return new ();
            return dict.TryGetValue(reviewId, out var list) ? list : new ();
        }

        public static IHtmlContent RenderReviewTags(DirectoryEntryReview r)
        {
            var links = r.ReviewTags;

            if (links == null || links.Count == 0)
                return HtmlString.Empty;

            var tags = links
                .Select(x => x.ReviewTag)
                .Where(t => t != null && t.IsEnabled)
                .OrderBy(t => t!.Name)
                .ToList();

            if (tags.Count == 0)
                return HtmlString.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("<div class=\"review-tags mt-2\">");

            foreach (var t in tags!)
            {
                var cls = ReviewTagUiHelper.BadgeClass(t.Level);
                var title = string.IsNullOrWhiteSpace(t.Description)
                    ? ""
                    : $" title=\"{WebUtility.HtmlEncode(t.Description)}\"";

                sb.Append($"<span class=\"badge {cls} me-1\"{title}>{WebUtility.HtmlEncode(t.Name)}</span>");
            }

            sb.Append("</div>");
            return new HtmlString(sb.ToString());
        }
    }
}
