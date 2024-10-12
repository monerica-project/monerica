using System.Text;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Helpers
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
                sb.Append(@" class=""sponsored blank_list_item"" ");
            }
            else
            {

                sb.Append(@" class=""blank_list_item"" ");
            }

            sb.Append(">");

            // Main content in a paragraph with the expandable label
            sb.Append("<p>");
            sb.AppendFormat(@"<label for=""{0}"" class=""expansion_item"">+</label>", checkboxId);

            // Handle date display options
            if (model.DateOption == DateDisplayOption.DisplayCreateDate)
            {
                sb.AppendFormat("<i>{0}</i> ", model.CreateDate.ToString(StringConstants.DateFormat));
            }
            else if (model.DateOption == DateDisplayOption.DisplayUpdateDate)
            {
                sb.AppendFormat("<i>{0}</i> ", (model.UpdateDate ?? model.CreateDate).ToString(StringConstants.DateFormat));
            }

            // Handle directory status and links
            if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Verified)
            {
                sb.Append("&#9989; ");
                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, false); // Use LinkA if sponsored
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false); // Use LinkA as fallback
                }
                else
                {
                    AppendLink(sb, model, model.Link, false); // Default to Link
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Admitted)
            {
                if ((model.IsSponsored || model.IsSubCategorySponsor) && !string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.Link, true); // Use LinkA if sponsored
                }
                else if (!string.IsNullOrWhiteSpace(model.LinkA))
                {
                    AppendLink(sb, model, model.LinkA, false); // Use LinkA as fallback
                }
                else
                {
                    AppendLink(sb, model, model.Link, false); // Default to Link
                }
            }
            else if (model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
            {
                sb.Append("&#10060; <del>");
                AppendLink(sb, model, model.Link, false, true); // Scam case
                sb.Append("</del>");
            }

            // Handle additional links (Link2 and Link3)
            if (model.LinkType != LinkType.ListingPage)
            {
                AppendAdditionalLinks(sb, model);
            }

            // Add description if available
            if (!string.IsNullOrWhiteSpace(model.Description))
            {
                sb.Append(" - ");
                sb.Append(model.Description); // Assuming it's safe HTML
            }

            // Add note if available
            if (!string.IsNullOrWhiteSpace(model.Note))
            {
                sb.AppendFormat(" <i>(Note: {0})</i> ", model.Note); // Assuming it's safe HTML
            }

            // Close the paragraph
            sb.Append("</p>");

            // Hidden checkbox that controls the expandable content
            sb.AppendFormat("<input type=\"checkbox\" id=\"{0}\" style=\"display:none;\" />", checkboxId);

            // Hidden div that will expand (additional content placeholder "TODO")
            sb.Append("<div class=\"hidden\">");
            sb.Append("<ul>");

            if (model.CreateDate == DateTime.MinValue)
            {
                sb.Append("<li>Added: N/A</li>");
            }
            else
            {
                sb.AppendFormat("<li>Added: {0}</li>", model.CreateDate.ToString(StringConstants.DateFormat));
            }

            if (model.UpdateDate != null)
            {
                sb.AppendFormat("<li>Updated: {0}</li>", model.UpdateDate?.ToString(StringConstants.DateFormat));
            }

            if (!string.IsNullOrWhiteSpace(model.Location))
            {
                sb.AppendFormat("<li>Location: {0}</li>", model.Location);
            }

            if (!string.IsNullOrWhiteSpace(model.Contact))
            {
                sb.AppendFormat("<li>Contact: {0}</li>", model.Contact);
            }

            sb.Append("</ul>");

            sb.Append("</div>");

            // Closing </li> tag
            sb.Append("</li>");

            return sb.ToString();
        }

        // Helper method for appending the main link
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

        // Helper method for appending additional links (Link2 and Link3)
        private static void AppendAdditionalLinks(StringBuilder sb, DirectoryEntryViewModel model)
        {
            AppendLinkWithSeparator(sb, model.Link2, model.Link2A, model.Link2Name, model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam);
            AppendLinkWithSeparator(sb, model.Link3, model.Link3A, model.Link3Name, model.DirectoryStatus == Data.Enums.DirectoryStatus.Scam);
        }

        // Helper method to append links with a separator (" | ")
        private static void AppendLinkWithSeparator(StringBuilder sb, string? link, string? affiliateLink, string linkName, bool isScam)
        {
            if (!string.IsNullOrWhiteSpace(link))
            {
                sb.Append(" | ");
                var actualLink = affiliateLink ?? link;
                if (isScam)
                {
                    sb.AppendFormat("<del><a rel=\"nofollow\" href=\"{0}\" target=\"_blank\">{1}</a></del>", actualLink, linkName);
                }
                else
                {
                    sb.AppendFormat("<a href=\"{0}\" target=\"_blank\">{1}</a>", actualLink, linkName);
                }
            }
        }

        // Helper method to append the link based on the logic
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