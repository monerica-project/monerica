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
        public static string GenerateDirectoryEntryHtml(DirectoryEntryViewModel model, string? rootUrl = null)
        {
            var sb = new StringBuilder();

            // Opening <li> tag with optional sponsored class
            sb.Append("<li");

            if (model.IsSponsored && model.DisplayAsSponsoredItem)
            {
                sb.Append(@" class=""sponsored"" ");
            }

            sb.Append(">");

            sb.Append("<p>");

            // Handle date display options
            if (model.DateOption == DateDisplayOption.DisplayCreateDate)
            {
                sb.AppendFormat("<i>{0}</i> ", model.CreateDate.ToString(Common.Constants.StringConstants.DateFormat));
            }
            else if (model.DateOption == DateDisplayOption.DisplayUpdateDate)
            {
                sb.AppendFormat("<i>{0}</i> ", (model.UpdateDate ?? model.CreateDate).ToString(Common.Constants.StringConstants.DateFormat));
            }

            // Handle directory status and links
            if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Verified)
            {
                sb.Append("&#9989; ");
                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, false, rootUrl);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false, rootUrl);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false, rootUrl);
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Admitted)
            {
                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, true, rootUrl);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false, rootUrl);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false, rootUrl);
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Questionable)
            {
                sb.Append("&#10067; ");

                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, true, rootUrl);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false, rootUrl);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false, rootUrl);
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
            {
                sb.Append("&#10060; <del>");
                AppendLink(sb, model, model.Link, false, true);
                sb.Append("</del>");
            }

            if (model.LinkType != LinkType.ListingPage)
            {
                AppendAdditionalLinks(sb, model);
            }

            if (model.LinkType == LinkType.ListingPage)
            {
                AppendExternalLinkIcon(sb, model);
            }

            if (model.ItemDisplayType != ItemDisplayType.Email)
            {
                sb.Append(BuildFlagImgTag(model.CountryCode, rootUrl));
            }

            if (!string.IsNullOrWhiteSpace(model.Description))
            {
                sb.Append(" - ");
                sb.Append(model.Description); // Assuming it's safe HTML
            }

            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                sb.AppendFormat(" <i>(Note: {0})</i> ", model.Note); // Assuming it's safe HTML
            }

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
                sb.Append("<li>");
                sb.AppendFormat(
                    "<pre>{0}:</pre>",
                    DateTime.ParseExact(group.Date, Common.Constants.StringConstants.DateFormat, CultureInfo.InvariantCulture)
                        .ToString(Common.Constants.StringConstants.DateFormat));

                sb.Append("<ul>");
                foreach (var entry in group.Entries)
                {
                    sb.Append("<li>");
                    sb.Append("<p class=\"small-font text-inline\">");

                    if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
                    {
                        sb.Append("<strong>Scam!</strong> - ");
                    }
                    else if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Questionable)
                    {
                        sb.Append("<strong>Questionable!</strong> - ");
                    }
                    else if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Verified)
                    {
                        sb.Append("<strong>Verified</strong> - ");
                    }

                    sb.Append(entry.Name);
                    sb.Append("</p>");
                    sb.Append(" - ");

                    // Use LinkA as href if available, otherwise fall back to Link, but display Link text
                    var href = !string.IsNullOrWhiteSpace(entry.LinkA) ? entry.LinkA : entry.Link;
                    sb.AppendFormat("<a target=\"_blank\" class=\"multi-line-text small-font\" href=\"{0}\">{1}</a>", href, entry.Link);

                    if (!string.IsNullOrWhiteSpace(entry.Description))
                    {
                        sb.Append(" - ");
                        sb.AppendFormat("<p class=\"small-font text-inline\">{0}</p>", entry.Description);
                    }

                    sb.Append("</li>");
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
            // ensure no trailing slash
            string domain = rootUrl?.TrimEnd('/') ?? string.Empty;

            var liClasses = model.IsSponsored
                  ? "search-result-item sponsored"
                  : "search-result-item";

            var sb = new StringBuilder();
            sb.Append($"<li class=\"{liClasses}\">");

            // 1) Name → profile link, and “Website” link inline
            var name = WebUtility.HtmlEncode(model.Name);

            // model.ItemPath should be something like "/category/subcategory/entrykey"
            var profRelative = model.ItemPath.StartsWith("/") ? model.ItemPath : "/" + model.ItemPath;
            var profUrl = $"{domain}{profRelative}";

            sb.Append("<p>");

            sb.Append(GetDirectoryStausIcon(model.DirectoryStatus)); // ✅❌

            sb.AppendFormat(
                "<strong><a class=\"no-app-link\" href=\"{1}\">{0}</a></strong>",
                name,
                profUrl);

            // Append flag immediately after name
            if (model.ItemDisplayType != ItemDisplayType.Email)
            {
                sb.Append("&nbsp;");
                sb.Append(BuildFlagImgTag(model.CountryCode, rootUrl));
            }

            sb.Append(" — ");

            // pick affiliate if available
            var websiteUrl = !string.IsNullOrWhiteSpace(model.LinkA) && !model.IsSponsored
                ? model.LinkA.Trim()
                : model.Link.Trim();

            if (!string.IsNullOrWhiteSpace(websiteUrl))
            {
                var direct = WebUtility.HtmlEncode(websiteUrl);
                sb.AppendFormat(
                    "<a href=\"{0}\" target=\"_blank\">Website</a>",
                    direct);
            }

            if (model.AverageRating.HasValue && model.ReviewCount > 0)
            {
                sb.Append("&nbsp;");
                AppendRatingStars(sb, model.AverageRating.Value, model.ReviewCount.Value);
            }

            sb.Append("</p>");

            // 2) Link2 / Link3 (e.g. Tor | I2P)
            if (!string.IsNullOrWhiteSpace(model.Link2) || !string.IsNullOrWhiteSpace(model.Link3))
            {
                sb.Append("<p>");
                if (!string.IsNullOrWhiteSpace(model.Link2))
                {
                    var l2 = WebUtility.HtmlEncode(model.Link2);
                    var t2 = WebUtility.HtmlEncode(model.Link2Name);
                    sb.AppendFormat("<a href=\"{0}\" target=\"_blank\">{1}</a>", l2, t2);
                }

                if (!string.IsNullOrWhiteSpace(model.Link3))
                {
                    var l3 = WebUtility.HtmlEncode(model.Link3);
                    var t3 = WebUtility.HtmlEncode(model.Link3Name);
                    sb.AppendFormat(" | <a href=\"{0}\" target=\"_blank\">{1}</a>", l3, t3);
                }

                sb.Append("</p>");
            }

            // 3) Category › Subcategory (as separate links)
            if (model.SubCategory != null)
            {
                string catKey = WebUtility.HtmlEncode(model.SubCategory.Category.CategoryKey);
                string subKey = WebUtility.HtmlEncode(model.SubCategory.SubCategoryKey);
                string catName = WebUtility.HtmlEncode(model.SubCategory.Category.Name);
                string subName = WebUtility.HtmlEncode(model.SubCategory.Name);

                var catUrl = $"{domain}/{catKey}";
                var subUrl = $"{domain}/{catKey}/{subKey}";

                sb.AppendFormat(
                    "<p><a class=\"no-app-link\" href=\"{0}\">{1}</a> &rsaquo; <a class=\"no-app-link\" href=\"{2}\">{3}</a></p>", catUrl, catName, subUrl, subName);
            }

            // 4) Description & Note
            if (!string.IsNullOrWhiteSpace(model.Description))
            {
                var desc = WebUtility.HtmlEncode(model.Description);
                sb.AppendFormat("<p>{0}", desc);

                if (!string.IsNullOrWhiteSpace(model.Note))
                {
                    sb.Append(" <i>Note: ");
                    sb.Append(model.Note);  // rendered raw HTML
                    sb.Append("</i>");
                }

                sb.Append("</p>");
            }

            sb.Append("</li>");
            return sb.ToString();
        }

        private static string GetDirectoryStausIcon(DirectoryStatus directoryStatus)
        {
            if (directoryStatus == Data.Enums.DirectoryStatus.Verified)
            {
                return "&#9989; ";
            }
            else if (directoryStatus == Data.Enums.DirectoryStatus.Admitted)
            {
                return string.Empty;
            }
            else if (directoryStatus == Data.Enums.DirectoryStatus.Questionable)
            {
                return "&#10067; ";
            }
            else if (directoryStatus == Data.Enums.DirectoryStatus.Scam)
            {
                return "&#10060;";
            }
            else
            {
                return string.Empty;
            }
        }

        private static void AppendExternalLinkIcon(StringBuilder sb, DirectoryEntryViewModel model)
        {
            if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
            {
                return;
            }

            var link = model.Link;

            if (!string.IsNullOrWhiteSpace(model.LinkA) && model.IsSponsored == false)
            {
                link = model.LinkA;
            }

            sb.AppendFormat(@" <a target=""_blank"" class=""external-link"" title=""{0}"" href=""{0}""></a> ", link);
        }

        private static void AppendRatingStars(StringBuilder sb, double avg, int count)
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

            sb.AppendFormat(
                "<span class=\"rating-text\"> {0:0.0}/5 ({1})</span>",
                avg,
                count);

            sb.Append("</span>");
        }

        /// <summary>
        /// Helper method for appending the main link.
        /// </summary>
        private static void AppendLink(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? link,
            bool isScam = false,
            string? rootUrl = null)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }

            string finalLink = model.LinkType == LinkType.ListingPage
                ? $"{rootUrl ?? string.Empty}{model.ItemPath}"
                : link;

            string target = model.LinkType == LinkType.Direct ? "target=\"_blank\"" : string.Empty;
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
                sb.AppendFormat("<a {3} href=\"{0}\" {1}>{2}</a>", finalLink, target, name, relAttr);
            }
            else
            {
                if (model.LinkType == LinkType.ListingPage)
                {
                    sb.AppendFormat("<a {3} title=\"Profile: {2}\" href=\"{0}\" {1}>{2}</a>", finalLink, target, name, relAttr);
                }
                else
                {
                    sb.AppendFormat("<a {3} title=\"{2}\" href=\"{0}\" {1}>{2}</a>", finalLink, target, name, relAttr);
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
                    finalUrl,
                    WebUtility.HtmlEncode(linkName),
                    relAttr);
            }
            else
            {
                sb.AppendFormat(
                    "<a {2} href=\"{0}\" target=\"_blank\">{1}</a>",
                    finalUrl,
                    WebUtility.HtmlEncode(linkName),
                    relAttr);
            }
        }

        /// <summary>
        /// Helper method for appending additional links (Link2 and Link3).
        /// </summary>
        private static void AppendAdditionalLinks(StringBuilder sb, DirectoryEntryViewModel model)
        {
            // compute once whether this is a scam item
            var isScam = model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam;

            // pass the model along so we can check sponsorship
            AppendLinkWithSeparator(sb, model, model.Link2, model.Link2A, model.Link2Name, isScam);
            AppendLinkWithSeparator(sb, model, model.Link3, model.Link3A, model.Link3Name, isScam);
        }

        private static string BuildFlagImgTag(string? countryCode, string? rootUrl = null)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                // preserve the original for alt/title
                var code = countryCode.Trim();
                var countryName = CountryHelper.GetCountryName(code);

                // use lowercase for the filename
                var file = code.ToLowerInvariant();

                // resolves “~/…” to “/your-app-root/…” (or just “/” if you’re at IIS root)
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

        /// <summary>
        /// Helper method to append the link based on the logic.
        /// </summary>
        private static void AppendLink(StringBuilder sb, DirectoryEntryViewModel model, string link, bool isSponsored = false, bool isScam = false)
        {
            string finalLink = model.LinkType == LinkType.ListingPage ? model.ItemPath : link;
            string target = model.LinkType == LinkType.Direct ? "target=\"_blank\"" : string.Empty;
            string name = model.Name;

            if (isScam)
            {
                sb.AppendFormat("<a rel=\"nofollow\" href=\"{0}\" {1}>{2}</a>", finalLink, target, name);
            }
            else
            {
                sb.AppendFormat("<a href=\"{0}\" {1}>{2}</a>", finalLink, target, name);
            }
        }
    }
}