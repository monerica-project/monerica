using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Models;
using System.Globalization;
using System.Net;
using System.Text;

namespace DirectoryManager.DisplayFormatting.Helpers
{
    public class DisplayMarkUpHelper
    {
        public static string GenerateDirectoryEntryHtml(DirectoryEntryViewModel model)
        {
            var sb = new StringBuilder();

            // Generate a unique ID for the checkbox (to avoid conflicts)
            var checkboxId = $"entry_checkbox_{model.DirectoryEntryId}_{model.ItemDisplayType.ToString()}";

            // Opening <li> tag with optional sponsored class
            sb.Append("<li");

            if (model.IsSponsored && model.DisplayAsSponsoredItem)
            {
                sb.Append(@" class=""sponsored"" ");
            }

            sb.Append(">");

            // Main content in a paragraph with the expandable label
            sb.Append("<p>");
            sb.AppendFormat(@"<label for=""{0}"" class=""expansion_item"">+</label>", checkboxId);

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
                    AppendLink(sb, model, model.Link, false);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false);
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Admitted)
            {
                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, true);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false);
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Questionable)
            {
                sb.Append("&#10067; ");

                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, true);
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false);
                }
                else
                {
                    AppendLink(sb, model, model.Link, false);
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

            // Hidden checkbox that controls the expandable content
            sb.AppendFormat("<input type=\"checkbox\" id=\"{0}\" class=\"hidden\" />", checkboxId);

            // Hidden div that will expand (additional content placeholder "TODO")
            sb.Append("<div class=\"hidden\">");
            sb.Append("<ul>");

            if (model.CreateDate == DateTime.MinValue)
            {
                sb.Append("<li>Added: N/A</li>");
            }
            else
            {
                sb.AppendFormat("<li>Added: {0}</li>", model.CreateDate.ToString(Common.Constants.StringConstants.DateFormat));
            }

            if (model.UpdateDate != null)
            {
                sb.AppendFormat("<li>Updated: {0}</li>", model.UpdateDate?.ToString(Common.Constants.StringConstants.DateFormat));
            }

            if (!string.IsNullOrWhiteSpace(model.Location))
            {
                sb.AppendFormat("<li>Location: {0}</li>", model.Location);
            }

            if (!string.IsNullOrWhiteSpace(model.Processor))
            {
                sb.AppendFormat("<li>Processor: {0}</li>", model.Processor);
            }

            if (!string.IsNullOrWhiteSpace(model.Contact))
            {
                sb.AppendFormat("<li class=\"multi-line-text\"> Contact: {0}</li>", model.Contact);
            }

            sb.Append("</ul>");

            sb.Append("</div>");

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
                    "<pre>{0} Additions:</pre>",
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

        public static string GenerateSearchResultHtml(DirectoryEntryViewModel model, string canonicalDomain)
        {
            // ensure no trailing slash
            string domain = canonicalDomain?.TrimEnd('/') ?? string.Empty;

            var sb = new StringBuilder();
            sb.Append("<li class=\"search-result-item\">");

            // 1) Name → profile link, and “Website” link inline
            var name = WebUtility.HtmlEncode(model.Name);
            // model.ItemPath should be something like "/category/subcategory/entrykey"
            var profRelative = model.ItemPath.StartsWith("/") ? model.ItemPath : "/" + model.ItemPath;
            var profUrl = $"{domain}{profRelative}";
            
            sb.Append("<p>");
            sb.Append(GetDirectoryStausIcon(model.DirectoryStatus)); // your helper for ✅❌ etc.
            sb.AppendFormat(
                "<strong><a class=\"no-app-link\" href=\"{1}\">{0}</a></strong> — ",
                name, profUrl);

            // pick affiliate if available
            var websiteUrl = !string.IsNullOrWhiteSpace(model.LinkA)
                ? model.LinkA.Trim()
                : model.Link.Trim();

            if (!string.IsNullOrWhiteSpace(websiteUrl))
            {
                var direct = WebUtility.HtmlEncode(websiteUrl);
                sb.AppendFormat(
                    "<a href=\"{0}\" target=\"_blank\">Website</a>",
                    direct);
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
                    "<p><a class=\"no-app-link\" href=\"{0}\">{1}</a> &rsaquo; <a class=\"no-app-link\" href=\"{2}\">{3}</a></p>",
                    catUrl, catName,
                    subUrl, subName);
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

        /// <summary>
        /// Helper method for appending the main link 
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="model"></param>
        /// <param name="affiliateLink"></param>
        /// <param name="isScam"></param>
        private static void AppendLink(StringBuilder sb, DirectoryEntryViewModel model, string? affiliateLink, bool isScam = false)
        {
            string link = model.LinkType == LinkType.ListingPage ? model.ItemPath : affiliateLink ?? model.Link;
            string target = model.LinkType == LinkType.Direct ? "target=\"_blank\"" : string.Empty;
            string name = model.Name;

            if (isScam)
            {
                sb.AppendFormat("<a rel=\"nofollow\" href=\"{0}\" {1}>{2}</a>", link, target, name);
            }
            else
            {
                sb.AppendFormat("<a href=\"{0}\" {1}>{2}</a>", link, target, name);
            }
        }

        /// <summary>
        /// Helper method for appending additional links (Link2 and Link3)
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="model"></param>
        private static void AppendAdditionalLinks(StringBuilder sb, DirectoryEntryViewModel model)
        {
            // compute once whether this is a scam item
            var isScam = model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam;

            // pass the model along so we can check sponsorship
            AppendLinkWithSeparator(sb, model, model.Link2, model.Link2A, model.Link2Name, isScam);
            AppendLinkWithSeparator(sb, model, model.Link3, model.Link3A, model.Link3Name, isScam);
        }

        /// <summary>
        /// Helper method to append links with a separator (" | ")
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="model"></param>
        /// <param name="link"></param>
        /// <param name="affiliateLink"></param>
        /// <param name="linkName"></param>
        /// <param name="isScam"></param>
        private static void AppendLinkWithSeparator(
            StringBuilder sb,
            DirectoryEntryViewModel model,
            string? link,
            string? affiliateLink,
            string linkName,
            bool isScam)
        {
            if (string.IsNullOrWhiteSpace(link))
                return;

            sb.Append(" | ");

            // if sponsored and we have a LinkA, use the non-A link; otherwise fall back to A or link
            var finalUrl = (model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(affiliateLink)
                ? link
                : affiliateLink ?? link;

            if (isScam)
            {
                sb.AppendFormat(
                    "<del><a rel=\"nofollow\" href=\"{0}\" target=\"_blank\">{1}</a></del>",
                    finalUrl,
                    linkName);
            }
            else
            {
                sb.AppendFormat(
                    "<a href=\"{0}\" target=\"_blank\">{1}</a>",
                    finalUrl,
                    linkName);
            }
        }


        /// <summary>
        /// Helper method to append the link based on the logic
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="model"></param>
        /// <param name="link"></param>
        /// <param name="isSponsored"></param>
        /// <param name="isScam"></param>
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