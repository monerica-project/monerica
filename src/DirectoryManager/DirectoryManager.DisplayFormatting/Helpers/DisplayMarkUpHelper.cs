using System.Globalization;
using System.Net;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    public class DisplayMarkUpHelper
    {
        // ----------------------------
        // Public API
        // ----------------------------

        public static string GenerateDirectoryEntryHtml(DirectoryEntryViewModel model, string? rootUrl = null)
        {
            var sb = new StringBuilder();

            AppendDirectoryEntryOpenLi(sb, model);
            sb.Append("<p>");

            AppendOptionalDate(sb, model);

            var effectiveLinkType = GetEffectiveLinkTypeForDirectoryEntryRender(model);
            AppendStatusAndPrimaryLink(sb, model, rootUrl, effectiveLinkType);

            AppendDirectoryEntryLinksBlock(sb, model, rootUrl, effectiveLinkType);

            AppendOptionalFlag(sb, model, rootUrl);
            AppendOptionalDescriptionAndNote(sb, model);

            // stars + "x.x/5" + clickable "(count)" -> profile#reviews
            AppendInlineStarRating(sb, model, rootUrl);

            sb.Append("</p>");
            sb.Append("</li>");

            return sb.ToString();
        }

        public static string GenerateGroupedDirectoryEntryHtml(IEnumerable<GroupedDirectoryEntry> groupedEntries)
        {
            var sb = new StringBuilder();
            sb.Append("<ul class=\"newest_items\">");

            foreach (var group in groupedEntries)
            {
                AppendGroupedDateHeader(sb, group);

                sb.Append("<ul>");
                foreach (var entry in group.Entries)
                {
                    AppendGroupedEntry(sb, entry);
                }
                sb.Append("</ul>");

                sb.Append("</li>");
                sb.AppendLine();
            }

            sb.Append("</ul>");
            return sb.ToString();
        }

        public static string GenerateSearchResultHtml(DirectoryEntryViewModel model, string rootUrl)
        {
            string domain = (rootUrl ?? string.Empty).TrimEnd('/');

            var liClasses = model.IsSponsored
                ? "search-result-item sponsored"
                : "search-result-item";

            var sb = new StringBuilder();
            sb.Append($"<li class=\"{liClasses}\">");

            var encodedName = WebUtility.HtmlEncode(model.Name);
            var profUrl = BuildProfileUrl(domain, model.ItemPath);
            var reviewsUrl = $"{profUrl}#reviews";

            sb.Append("<p>");

            sb.Append(GetDirectoryStausIcon(model.DirectoryStatus));
            AppendProfileName(sb, encodedName, profUrl);
            AppendFlagAfterName(sb, model, rootUrl);

            sb.Append(" — ");

            // Website | Tor | I2P on SAME line (only those which exist)
            AppendSearchResultLinksLine(sb, model);

            // stars + "x.x/5" + clickable "(count)" -> profile#reviews
            AppendSearchResultRating(sb, model, reviewsUrl);

            sb.Append("</p>");

            AppendCategoryBreadcrumbLine(sb, model, domain);
            AppendDescriptionNoteLine(sb, model);

            sb.Append("</li>");
            return sb.ToString();
        }

        // ----------------------------
        // DirectoryEntryHtml helpers
        // ----------------------------
        private static void AppendGroupedDateHeader(StringBuilder sb, GroupedDirectoryEntry group)
        {
            sb.Append("<li>");
            sb.AppendFormat(
                "<pre>{0}:</pre>",
                DateTime.ParseExact(group.Date, Common.Constants.StringConstants.DateFormat, CultureInfo.InvariantCulture)
                    .ToString(Common.Constants.StringConstants.DateFormat));
        }

        private static void AppendGroupedEntry(StringBuilder sb, DirectoryManager.Data.Models.DirectoryEntry entry)
        {
            sb.Append("<li>");
            sb.Append("<p class=\"small-font text-inline\">");

            AppendGroupedStatusLabel(sb, entry.DirectoryStatus);

            sb.Append(WebUtility.HtmlEncode(entry.Name));
            sb.Append("</p>");
            sb.Append(" - ");

            // Use LinkA as href if available, otherwise fall back to Link, but display Link text
            var href = !string.IsNullOrWhiteSpace(entry.LinkA) ? entry.LinkA : entry.Link;

            sb.AppendFormat(
                "<a target=\"_blank\" class=\"multi-line-text small-font\" href=\"{0}\">{1}</a>",
                WebUtility.HtmlEncode(href),
                WebUtility.HtmlEncode(entry.Link));

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                sb.Append(" - ");
                sb.AppendFormat(
                    "<p class=\"small-font text-inline\">{0}</p>",
                    WebUtility.HtmlEncode(entry.Description));
            }

            sb.Append("</li>");
        }

        private static void AppendDirectoryEntryOpenLi(StringBuilder sb, DirectoryEntryViewModel model)
        {
            sb.Append("<li");
            if (model.IsSponsored && model.DisplayAsSponsoredItem)
            {
                sb.Append(@" class=""sponsored"" ");
            }
            sb.Append(">");
        }

        private static void AppendOptionalDate(StringBuilder sb, DirectoryEntryViewModel model)
        {
            if (model.DateOption == DateDisplayOption.DisplayCreateDate)
            {
                sb.AppendFormat("<i>{0}</i> ",
                    model.CreateDate.ToString(Common.Constants.StringConstants.DateFormat));
            }
            else if (model.DateOption == DateDisplayOption.DisplayUpdateDate)
            {
                sb.AppendFormat("<i>{0}</i> ",
                    (model.UpdateDate ?? model.CreateDate).ToString(Common.Constants.StringConstants.DateFormat));
            }
        }

        /// <summary>
        /// When IsSubCategorySponsor is true, render like category sponsor (ListingPage style).
        /// </summary>
        private static LinkType GetEffectiveLinkTypeForDirectoryEntryRender(DirectoryEntryViewModel model)
        {
            return model.IsSubCategorySponsor ? LinkType.Direct : model.LinkType;
        }

        private static void AppendStatusAndPrimaryLink(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? rootUrl,
            LinkType effectiveLinkType)
        {
            string? primary = GetPrimaryUrlForDisplay(model);

            if (model.DirectoryStatus == DirectoryStatus.Verified)
            {
                sb.Append("&#9989; ");
                AppendPrimaryLinkForDirectoryEntry(sb, model, primary, rootUrl, isScam: false, effectiveLinkType);
            }
            else if (model.DirectoryStatus == DirectoryStatus.Admitted)
            {
                AppendPrimaryLinkForDirectoryEntry(sb, model, primary, rootUrl, isScam: false, effectiveLinkType);
            }
            else if (model.DirectoryStatus == DirectoryStatus.Questionable)
            {
                sb.Append("&#10067; ");
                AppendPrimaryLinkForDirectoryEntry(sb, model, primary, rootUrl, isScam: false, effectiveLinkType);
            }
            else if (model.DirectoryStatus == DirectoryStatus.Scam)
            {
                sb.Append("&#10060; <del>");
                AppendLink(sb, model, model.Link, isScam: true, rootUrl: rootUrl, effectiveLinkType: effectiveLinkType);
                sb.Append("</del>");
            }
        }

        private static void AppendPrimaryLinkForDirectoryEntry(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? primaryUrl,
            string? rootUrl,
            bool isScam,
            LinkType effectiveLinkType)
        {
            // Preserve your existing sponsorship/affiliate rules
            if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
            {
                AppendLink(sb, model, model.Link, isScam: isScam, rootUrl: rootUrl, effectiveLinkType: effectiveLinkType);
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.LinkA))
            {
                AppendLink(sb, model, model.LinkA, isScam: isScam, rootUrl: rootUrl, effectiveLinkType: effectiveLinkType);
                return;
            }

            AppendLink(sb, model, primaryUrl ?? model.Link, isScam: isScam, rootUrl: rootUrl, effectiveLinkType: effectiveLinkType);
        }

        private static void AppendDirectoryEntryLinksBlock(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? rootUrl,
            LinkType effectiveLinkType)
        {
            // For directory list view:
            // - Non ListingPage: show Link2 | Link3 inline
            // - ListingPage: show external link icon
            if (effectiveLinkType != LinkType.ListingPage)
            {
                AppendAdditionalLinks(sb, model);
            }
            else
            {
                AppendExternalLinkIcon(sb, model);
            }
        }

        private static void AppendOptionalFlag(StringBuilder sb, DirectoryEntryViewModel model, string? rootUrl)
        {
            if (model.ItemDisplayType != ItemDisplayType.Email)
            {
                sb.Append(BuildFlagImgTag(model.CountryCode, rootUrl));
            }
        }

        private static void AppendOptionalDescriptionAndNote(StringBuilder sb, DirectoryEntryViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Description))
            {
                sb.Append(" - ");
                sb.Append(model.Description); // assumes safe HTML upstream
            }

            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                sb.AppendFormat(" <i>(Note: {0})</i> ", model.Note); // assumes safe HTML upstream
            }
        }
 
        // ----------------------------
        // SearchResult helpers
        // ----------------------------

        private static string BuildProfileUrl(string domain, string? itemPath)
        {
            var path = (itemPath ?? string.Empty).Trim();
            if (!path.StartsWith("/")) path = "/" + path;
            return string.IsNullOrEmpty(domain) ? path : $"{domain}{path}";
        }

        private static void AppendProfileName(StringBuilder sb, string encodedName, string profUrl)
        {
            sb.AppendFormat(
                "<strong><a class=\"no-app-link\" href=\"{1}\">{0}</a></strong>",
                encodedName,
                WebUtility.HtmlEncode(profUrl));
        }

        private static void AppendFlagAfterName(StringBuilder sb, DirectoryEntryViewModel model, string rootUrl)
        {
            if (model.ItemDisplayType != ItemDisplayType.Email)
            {
                sb.Append("&nbsp;");
                sb.Append(BuildFlagImgTag(model.CountryCode, rootUrl));
            }
        }

        /// <summary>
        /// Emits: Website | Tor | I2P (only those which exist)
        /// </summary>
        private static void AppendSearchResultLinksLine(StringBuilder sb, DirectoryEntryViewModel model)
        {
            bool wroteAny = false;

            // Website uses affiliate (LinkA) when NOT sponsored (your current rule)
            var websiteUrl = (!string.IsNullOrWhiteSpace(model.LinkA) && !model.IsSponsored)
                ? model.LinkA.Trim()
                : (model.Link ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(websiteUrl))
            {
                AppendInlineLink(sb, "Website", websiteUrl, ref wroteAny);
            }

            if (!string.IsNullOrWhiteSpace(model.Link2))
            {
                var label = string.IsNullOrWhiteSpace(model.Link2Name) ? "Tor" : model.Link2Name;
                AppendInlineLink(sb, label, model.Link2.Trim(), ref wroteAny);
            }

            if (!string.IsNullOrWhiteSpace(model.Link3))
            {
                var label = string.IsNullOrWhiteSpace(model.Link3Name) ? "I2P" : model.Link3Name;
                AppendInlineLink(sb, label, model.Link3.Trim(), ref wroteAny);
            }
        }

        private static void AppendInlineLink(StringBuilder sb, string label, string url, ref bool wroteAny)
        {
            if (wroteAny) sb.Append(" | ");

            sb.Append("<a href=\"");
            sb.Append(WebUtility.HtmlEncode(url));
            sb.Append("\" target=\"_blank\">");
            sb.Append(WebUtility.HtmlEncode(label));
            sb.Append("</a>");

            wroteAny = true;
        }

        private static void AppendSearchResultRating(StringBuilder sb, DirectoryEntryViewModel model, string reviewsUrl)
        {
            if (model.AverageRating.HasValue && model.ReviewCount.HasValue && model.ReviewCount.Value > 0)
            {
                sb.Append("&nbsp;");
                AppendRatingStars(sb, model.AverageRating.Value, model.ReviewCount.Value, reviewsUrl);
            }
        }

        private static void AppendCategoryBreadcrumbLine(StringBuilder sb, DirectoryEntryViewModel model, string domain)
        {
            if (model.SubCategory == null) return;

            string catKey = WebUtility.HtmlEncode(model.SubCategory.Category.CategoryKey);
            string subKey = WebUtility.HtmlEncode(model.SubCategory.SubCategoryKey);
            string catName = WebUtility.HtmlEncode(model.SubCategory.Category.Name);
            string subName = WebUtility.HtmlEncode(model.SubCategory.Name);

            var catUrl = $"{domain}/{catKey}";
            var subUrl = $"{domain}/{catKey}/{subKey}";

            sb.AppendFormat(
                "<p><a class=\"no-app-link\" href=\"{0}\">{1}</a> &rsaquo; <a class=\"no-app-link\" href=\"{2}\">{3}</a></p>",
                WebUtility.HtmlEncode(catUrl), catName, WebUtility.HtmlEncode(subUrl), subName);
        }

        private static void AppendDescriptionNoteLine(StringBuilder sb, DirectoryEntryViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Description)) return;

            var desc = WebUtility.HtmlEncode(model.Description);
            sb.AppendFormat("<p>{0}", desc);

            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                sb.Append(" <i>Note: ");
                sb.Append(model.Note); // raw HTML on purpose (your current behavior)
                sb.Append("</i>");
            }

            sb.Append("</p>");
        }

        // ----------------------------
        // Shared helpers (status, stars, links, flags)
        // ----------------------------

        private static void AppendGroupedStatusLabel(StringBuilder sb, DirectoryStatus status)
        {
            if (status == DirectoryStatus.Scam)
            {
                sb.Append("<strong>Scam!</strong> - ");
            }
            else if (status == DirectoryStatus.Questionable)
            {
                sb.Append("<strong>Questionable!</strong> - ");
            }
            else if (status == DirectoryStatus.Verified)
            {
                sb.Append("<strong>Verified</strong> - ");
            }
        }

        private static string GetDirectoryStausIcon(DirectoryStatus directoryStatus)
        {
            return directoryStatus switch
            {
                DirectoryStatus.Verified => "&#9989; ",
                DirectoryStatus.Questionable => "&#10067; ",
                DirectoryStatus.Scam => "&#10060;",
                _ => string.Empty
            };
        }

        private static void AppendExternalLinkIcon(StringBuilder sb, DirectoryEntryViewModel model)
        {
            if (model.DirectoryStatus == DirectoryStatus.Scam)
            {
                return;
            }

            var link = !string.IsNullOrWhiteSpace(model.LinkA) && model.IsSponsored == false
                ? model.LinkA
                : model.Link;

            sb.AppendFormat(
                @" <a target=""_blank"" class=""external-link"" title=""{0}"" href=""{0}""></a> ",
                WebUtility.HtmlEncode(link));
        }

        /// <summary>
        /// Stars: stars x.x/5 (count)
        /// where ONLY (count) is a link to the internal profile #reviews.
        /// </summary>
        private static void AppendInlineStarRating(StringBuilder sb, DirectoryEntryViewModel model, string? rootUrl = null)
        {
            var count = model.ReviewCount ?? 0;

            if (!model.AverageRating.HasValue || count <= 0)
            {
                return;
            }

            string? reviewsUrl = BuildReviewsUrl(rootUrl, model.ItemPath);
            sb.Append("&nbsp;");
            AppendRatingStars(sb, model.AverageRating.Value, count, reviewsUrl);
        }

        private static string? BuildReviewsUrl(string? rootUrl, string? itemPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath)) return null;

            var baseUrl = (rootUrl ?? string.Empty).TrimEnd('/');
            var path = itemPath.StartsWith("/") ? itemPath : "/" + itemPath;

            return string.IsNullOrEmpty(baseUrl)
                ? $"{path}#reviews"
                : $"{baseUrl}{path}#reviews";
        }

        // Links ONLY the (count) to #reviews when url provided
        private static void AppendRatingStars(StringBuilder sb, double avg, int count, string? reviewsUrl)
        {
            const int totalStars = 5;
            double percent = Math.Clamp(avg / totalStars * 100, 0, 100);

            sb.Append("<span class=\"rating-wrapper\" aria-label=\"");
            sb.Append($"{avg:0.0} out of 5 stars from {count} reviews\">");

            sb.Append("<span class=\"stars\">");
            sb.Append("<span class=\"stars-fill\" style=\"width:");
            sb.Append(percent.ToString("0.##", CultureInfo.InvariantCulture));
            sb.Append("%\"></span>");
            sb.Append("</span>");

            // x.x/5 (NOT a link)
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "<span class=\"rating-text\"> {0:0.0}/5 </span>",
                avg);

            // (count) is a link if we have a URL
            if (!string.IsNullOrWhiteSpace(reviewsUrl))
            {
                sb.Append("<a class=\"rating-count-link\" href=\"");
                sb.Append(WebUtility.HtmlEncode(reviewsUrl));
                sb.Append("\">(");
                sb.Append(count.ToString(CultureInfo.InvariantCulture));
                sb.Append(")</a>");
            }
            else
            {
                sb.Append("<span class=\"rating-count\">(");
                sb.Append(count.ToString(CultureInfo.InvariantCulture));
                sb.Append(")</span>");
            }

            sb.Append("</span>");
        }

        private static string? GetPrimaryUrlForDisplay(DirectoryEntryViewModel model)
        {
            // Mirrors your earlier logic: prefer LinkA in some cases, else Link
            if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
            {
                return model.Link; // sponsored/sub-sponsor display uses Link (non-affiliate) for visible click
            }

            if (!string.IsNullOrWhiteSpace(model.LinkA))
            {
                return model.LinkA;
            }

            return model.Link;
        }

        /// <summary>
        /// Appends the main link (supports ListingPage relative + rel attributes for sponsored/scam).
        /// effectiveLinkType overrides model.LinkType when needed (subcategory sponsor render).
        /// </summary>
        private static void AppendLink(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? link,
            bool isScam = false,
            string? rootUrl = null,
            LinkType? effectiveLinkType = null)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }

            var lt = effectiveLinkType ?? model.LinkType;

            string finalLink = lt == LinkType.ListingPage
                ? $"{rootUrl ?? string.Empty}{model.ItemPath}"
                : link;

            string target = lt == LinkType.Direct ? "target=\"_blank\"" : string.Empty;
            string name = WebUtility.HtmlEncode(model.Name);

            // build rel attribute
            var relParts = new List<string>();
            if (isScam)
            {
                relParts.Add("nofollow");
            }

            if (model.IsSponsored)
            {
                relParts.Add("sponsored");
            }

            string relAttr = relParts.Count > 0
                ? $"rel=\"{string.Join(" ", relParts)}\""
                : string.Empty;

            if (isScam)
            {
                sb.AppendFormat("<a {3} href=\"{0}\" {1}>{2}</a>",
                    WebUtility.HtmlEncode(finalLink), target, name, relAttr);
            }
            else
            {
                if (lt == LinkType.ListingPage)
                {
                    sb.AppendFormat("<a {3} title=\"Profile: {2}\" href=\"{0}\" {1}>{2}</a>",
                        WebUtility.HtmlEncode(finalLink), target, name, relAttr);
                }
                else
                {
                    sb.AppendFormat("<a {3} title=\"{2}\" href=\"{0}\" {1}>{2}</a>",
                        WebUtility.HtmlEncode(finalLink), target, name, relAttr);
                }
            }
        }

        private static void AppendLinkWithSeparator(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? link,
            string? affiliateLink,
            string linkName,
            bool isScam)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }

            sb.Append(" | ");

            var finalUrl = (model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(affiliateLink)
                ? link
                : affiliateLink ?? link;

            // build rel attribute
            var relParts = new List<string>();
            if (isScam)
            {
                relParts.Add("nofollow");
            }

            if (model.IsSponsored)
            {
                relParts.Add("sponsored");
            }

            string relAttr = relParts.Count > 0
                ? $"rel=\"{string.Join(" ", relParts)}\""
                : string.Empty;

            if (isScam)
            {
                sb.AppendFormat(
                    "<del><a {2} href=\"{0}\" target=\"_blank\">{1}</a></del>",
                    WebUtility.HtmlEncode(finalUrl),
                    WebUtility.HtmlEncode(linkName),
                    relAttr);
            }
            else
            {
                sb.AppendFormat(
                    "<a {2} href=\"{0}\" target=\"_blank\">{1}</a>",
                    WebUtility.HtmlEncode(finalUrl),
                    WebUtility.HtmlEncode(linkName),
                    relAttr);
            }
        }

        /// <summary>
        /// Appends additional links (Link2 and Link3) for the directory list view (non-search).
        /// </summary>
        private static void AppendAdditionalLinks(StringBuilder sb, DirectoryEntryViewModel model)
        {
            var isScam = model.DirectoryStatus == DirectoryStatus.Scam;

            AppendLinkWithSeparator(sb, model, model.Link2, model.Link2A, model.Link2Name, isScam);
            AppendLinkWithSeparator(sb, model, model.Link3, model.Link3A, model.Link3Name, isScam);
        }

        private static string BuildFlagImgTag(string? countryCode, string? rootUrl = null)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var code = countryCode.Trim();
                var countryName = CountryHelper.GetCountryName(code);
                var file = code.ToLowerInvariant();

                var src = string.Concat(rootUrl, $"/images/flags/{file}.png");

                sb.Append("<img")
                  .Append(" class=\"country-flag\"")
                  .Append(" src=\"").Append(src).Append('"')
                  .Append(" alt=\"Flag of ").Append(countryName).Append('"')
                  .Append(" title=\"").Append(countryName).Append('"')
                  .Append(" />");
            }

            return sb.ToString();
        }
    }
}
