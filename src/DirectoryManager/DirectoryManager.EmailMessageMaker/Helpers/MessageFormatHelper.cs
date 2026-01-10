using System.Text;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.EmailMessageMaker.Helpers
{
    public class MessageFormatHelper
    {
        private const string RootUrl = "https://monerica.com";

        private const string EmailCss = @"
            body {
                font-family: Arial, sans-serif;
                line-height: 1.6;
                color: #333333;
                margin: 0;
                padding: 0;
            }
            .container {
                max-width: 600px;
                margin: 0 auto;
                padding: 20px;
            }
            .header {
                background-color: #f4f4f4;
                padding: 10px 20px;
                text-align: center;
            }
            .footer {
                text-align: center;
                font-size: 12px;
                color: #777777;
                margin-top: 20px;
            }
            .button {
                display: inline-block;
                padding: 10px 20px;
                margin: 20px 0;
                font-size: 16px;
                color: #ffffff !important;
                background-color: #28a745;
                text-decoration: none;
                border-radius: 5px;
                text-align: center;
                font-weight: bold;
            }
            .button:hover {
                background-color: #218838;
            }
            ul {
                padding-left: 20px;
            }
            li {
                margin-bottom: 10px;
            }
            a {
                color: #007bff;
                text-decoration: none;
            }
            a:hover {
                text-decoration: underline;
            }

            /* Miscellaneous */
            .hidden {
                display: none;
            }
            
            :checked + .hidden {
                display: block;
            }

            ul.blank_list_item li {
                list-style-type: none;
            }
            
            label.expansion_item {
                cursor: pointer;
                padding-right: 5px;
            }
        ";

        public static string GenerateHtmlEmail(
            IEnumerable<DirectoryEntry> newEntries,
            IEnumerable<SponsoredListing> mainSponsors,
            IEnumerable<SponsoredListing> categorySponsors,
            IEnumerable<SponsoredListing> subCategorySponsors,
            string siteName = "",
            string footerHtml = "",
            string link2Name = "",
            string link3Name = "")
        {
            var result = new StringBuilder();

            result.AppendLine("<!DOCTYPE html>");
            result.AppendLine("<html>");
            result.AppendLine("<head>");
            result.AppendLine("<meta charset=\"UTF-8\">");
            result.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            result.AppendLine($"<style>{EmailCss}</style>");
            result.AppendLine("</head>");
            result.AppendLine("<body>");
            result.AppendLine("<div class=\"container\">");

            // Header Section
            result.AppendLine("<div class=\"header\">");

            if (string.IsNullOrWhiteSpace(siteName))
            {
                result.AppendLine($"<h1>Directory Updates</h1>");
            }
            else
            {
                result.AppendLine($"<h1>{siteName} - Directory Updates</h1>");
            }

            result.AppendLine("</div>");

            // New Entries Section
            result.AppendLine("<h1>New Entries from Last Week</h1>");
            if (newEntries.Any())
            {
                result.AppendLine("<div>");
                var groupedEntries = newEntries
                    .Where(e => e.SubCategory?.Category != null)
                    .GroupBy(e => e.SubCategory!.Category!.Name)
                    .OrderBy(g => g.Key);

                foreach (var categoryGroup in groupedEntries)
                {
                    result.AppendLine($"<h2>{categoryGroup.Key}</h2>");
                    foreach (var subCategoryGroup in categoryGroup
                        .GroupBy(e => e.SubCategory!.Name)
                        .OrderBy(g => g.Key))
                    {
                        result.AppendLine($"<h3>{subCategoryGroup.Key}</h3>");
                        result.AppendLine("<ul class=\"blank_list_item\">");
                        foreach (var entry in subCategoryGroup.OrderBy(e => e.Name))
                        {
                            // 1) Build the view model as before
                            var viewModels = ViewModelConverter.ConvertToViewModels(
                                                new List<DirectoryEntry> { entry },
                                                DateDisplayOption.NotDisplayed,
                                                ItemDisplayType.Email,
                                                link2Name,
                                                link3Name)
                                                .ToList();

                            var vm = viewModels.First();

                            // 2) Force the email version to use the listing page URL
                            vm.LinkType = LinkType.ListingPage;

                            // 3) Strip Tor/I2P (Link2/Link3) for email to keep links on monerica.com
                            vm.Link2 = null;
                            vm.Link2A = null;
                            vm.Link3 = null;
                            vm.Link3A = null;

                            // 4) Generate HTML using the root domain so all links are monerica.com/...
                            var htmlItem = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(vm, RootUrl);

                            result.AppendLine(htmlItem);
                        }

                        result.AppendLine("</ul>");
                    }
                }

                result.AppendLine("</div>");
            }
            else
            {
                result.AppendLine("<p>No new entries this week.</p>");
            }

            // Main Sponsors Section (also link to listing pages only)
            AppendMainSponsors(mainSponsors, result, link2Name, link3Name);

            // Category Sponsors Section
            AppendCategorySponsors(categorySponsors, result, link2Name, link3Name);

            // Subcategory Sponsors Section
            AppendSubCategorySponsors(subCategorySponsors, result, link2Name, link3Name);

            // Footer
            if (!string.IsNullOrEmpty(footerHtml))
            {
                result.AppendLine("<div class=\"footer\">");
                result.AppendLine(footerHtml);
                result.AppendLine("</div>");
            }

            result.AppendLine("</div>");
            result.AppendLine("</body>");
            result.AppendLine("</html>");

            return result.ToString();
        }

        // --- TEXT VERSIONS BELOW ARE UNCHANGED (you can later swap links to listing pages there too) ---

        public static string GenerateDirectoryEntryText(IEnumerable<DirectoryEntry> entries)
        {
            var result = new StringBuilder();

            // Group and order entries by Category and SubCategory, then by Name
            var groupedEntries = entries
                .Where(e => e.SubCategory?.Category != null) // Exclude entries without Category or SubCategory
                .GroupBy(e => e.SubCategory!.Category!.Name) // Group by Category
                .OrderBy(g => g.Key); // Sort Categories alphabetically

            foreach (var categoryGroup in groupedEntries)
            {
                // Print Category name with a blank line after
                result.AppendLine(categoryGroup.Key);
                result.AppendLine();

                // Group by SubCategory within the Category
                var subCategoryGroups = categoryGroup
                    .GroupBy(e => e.SubCategory!.Name)
                    .OrderBy(g => g.Key); // Sort SubCategories alphabetically

                foreach (var subCategoryGroup in subCategoryGroups)
                {
                    // Print SubCategory name with indentation
                    result.AppendLine($"  {subCategoryGroup.Key}");
                    result.AppendLine();

                    // List entries in alphabetical order
                    foreach (var entry in subCategoryGroup.OrderBy(e => e.Name))
                    {
                        if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Scam)
                        {
                            result.AppendLine($"     + Scam! - {entry.Name} - {entry.Link} - {entry.Description}{(string.IsNullOrWhiteSpace(entry.Note) ? "" : $" <i>({entry.Note})</i>")}".TrimEnd());
                        }
                        else if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Questionable)
                        {
                            result.AppendLine($"     + Questionable! - {entry.Name} - {entry.Link} - {entry.Description}{(string.IsNullOrWhiteSpace(entry.Note) ? "" : $" <i>({entry.Note})</i>")}".TrimEnd());
                        }
                        else if (entry.DirectoryStatus == Data.Enums.DirectoryStatus.Verified)
                        {
                            result.AppendLine($"     + Verified - {entry.Name} - {entry.Link} - {entry.Description}{(string.IsNullOrWhiteSpace(entry.Note) ? "" : $" <i>({entry.Note})</i>")}".TrimEnd());
                        }
                        else
                        {
                            result.AppendLine($"     + {entry.Name} - {entry.Link} - {entry.Description}{(string.IsNullOrWhiteSpace(entry.Note) ? "" : $" <i>({entry.Note})</i>")}".TrimEnd());
                        }
                    }

                    // Add a blank line after each SubCategory
                    result.AppendLine();
                }
            }

            return result.ToString().TrimEnd(); // Ensure only one trailing blank line at the end
        }

        public static string GenerateMainSponsorText(IEnumerable<SponsoredListing> mainSponsors)
        {
            var result = new StringBuilder();

            if (mainSponsors.Any())
            {
                result.AppendLine();
                result.AppendLine("Main Sponsors");
                result.AppendLine();
            }

            foreach (var sponsor in mainSponsors)
            {
                if (sponsor.DirectoryEntry != null)
                {
                    result.AppendLine($"+ {sponsor.DirectoryEntry.Name} - {sponsor.DirectoryEntry.Link} - {sponsor.DirectoryEntry.Description}");
                }
            }

            return result.ToString().TrimEnd();
        }

        public static string GenerateCategorySponsorText(IEnumerable<SponsoredListing> categorySponsors)
        {
            var result = new StringBuilder();

            if (categorySponsors.Any())
            {
                result.AppendLine();
                result.AppendLine("Category Sponsors");
                result.AppendLine();
            }

            foreach (var sponsor in categorySponsors)
            {
                if (sponsor.DirectoryEntry != null)
                {
                    result.AppendLine($"{sponsor.DirectoryEntry?.SubCategory?.Category.Name}");
                    result.AppendLine($"+ {sponsor.DirectoryEntry?.Name} - {sponsor.DirectoryEntry?.Link} - {sponsor.DirectoryEntry?.Description}");
                }
            }

            return result.ToString().TrimEnd();
        }

        public static string GenerateSubcategorySponsorText(IEnumerable<SponsoredListing> subCategorySponsors)
        {
            var result = new StringBuilder();

            if (subCategorySponsors.Any())
            {
                result.AppendLine();
                result.AppendLine("Subcategory Sponsors");
                result.AppendLine();
            }

            foreach (var sponsor in subCategorySponsors)
            {
                if (sponsor.DirectoryEntry != null)
                {
                    result.AppendLine($"{sponsor.DirectoryEntry?.SubCategory?.Category.Name} > {sponsor.DirectoryEntry?.SubCategory?.Name}");
                    result.AppendLine($"+ {sponsor.DirectoryEntry?.Name} - {sponsor.DirectoryEntry?.Link} - {sponsor.DirectoryEntry?.Description}");
                }
            }

            return result.ToString().TrimEnd();
        }

        public static string GenerateTextEmail(
            List<DirectoryEntry> entries,
            List<SponsoredListing> mainSponsors,
            List<SponsoredListing> categorySponsors,
            List<SponsoredListing> subCategorySponsors,
            string emailSettingUnsubscribeFooterText)
        {
            var sb = new StringBuilder();

            sb.AppendLine(GenerateDirectoryEntryText(entries));

            sb.AppendLine("-----------------------------------");

            sb.AppendLine(GenerateMainSponsorText(mainSponsors));

            sb.AppendLine(GenerateCategorySponsorText(categorySponsors));

            sb.AppendLine(GenerateSubcategorySponsorText(subCategorySponsors));

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("-----------------------------------");
            sb.AppendLine(emailSettingUnsubscribeFooterText);

            return sb.ToString();
        }

        // ------- UPDATED SPONSOR HTML HELPERS (listing page links only) -------

        private static void AppendMainSponsors(IEnumerable<SponsoredListing> mainSponsors, StringBuilder result, string link2Name, string link3Name)
        {
            if (mainSponsors.Any())
            {
                result.AppendLine("<hr />");
                result.AppendLine("<h1>Main Sponsors</h1>");
                result.AppendLine("<ul>");
                foreach (var sponsor in mainSponsors)
                {
                    AppendEntry(result, sponsor, link2Name, link3Name);
                }
                result.AppendLine("</ul>");
            }
        }

        private static void AppendCategorySponsors(IEnumerable<SponsoredListing> categorySponsors, StringBuilder result, string link2Name, string link3Name)
        {
            if (categorySponsors.Any())
            {
                result.AppendLine("<hr />");
                result.AppendLine("<h1>Category Sponsors</h1>");

                var groupedSponsors = categorySponsors
                    .Where(s => s.DirectoryEntry?.SubCategory?.Category != null)
                    .GroupBy(s => new
                    {
                        Category = s.DirectoryEntry!.SubCategory!.Category!.Name,
                        SubCategory = s.DirectoryEntry!.SubCategory!.Name
                    })
                    .OrderBy(g => g.Key.Category)
                    .ThenBy(g => g.Key.SubCategory);

                foreach (var group in groupedSponsors)
                {
                    result.AppendLine($"<h2>{group.Key.Category}</h2>");
                    result.AppendLine("<ul>");

                    foreach (var sponsor in group)
                    {
                        AppendEntry(result, sponsor, link2Name, link3Name);
                    }

                    result.AppendLine("</ul>");
                }
            }
        }

        private static void AppendSubCategorySponsors(IEnumerable<SponsoredListing> subCategorySponsors, StringBuilder result, string link2Name, string link3Name)
        {
            if (subCategorySponsors.Any())
            {
                result.AppendLine("<hr />");
                result.AppendLine("<h1>Subcategory Sponsors</h1>");

                var groupedSponsors = subCategorySponsors
                    .Where(s => s.DirectoryEntry?.SubCategory?.Category != null)
                    .GroupBy(s => new
                    {
                        Category = s.DirectoryEntry!.SubCategory!.Category!.Name,
                        SubCategory = s.DirectoryEntry!.SubCategory!.Name
                    })
                    .OrderBy(g => g.Key.Category)
                    .ThenBy(g => g.Key.SubCategory);

                foreach (var group in groupedSponsors)
                {
                    result.AppendLine($"<h2>{group.Key.Category} &rsaquo; {group.Key.SubCategory}</h2>");
                    result.AppendLine("<ul>");

                    foreach (var sponsor in group)
                    {
                        AppendEntry(result, sponsor, link2Name, link3Name);
                    }

                    result.AppendLine("</ul>");
                }
            }
        }

        /// <summary>
        /// For HTML sponsor sections, render each sponsor via the same ViewModel + DisplayMarkUpHelper
        /// so they also only link to the listing page.
        /// </summary>
        private static void AppendEntry(StringBuilder result, SponsoredListing sponsor, string link2Name, string link3Name)
        {
            if (sponsor.DirectoryEntry == null)
            {
                return;
            }

            var vms = ViewModelConverter.ConvertToViewModels(
                new List<DirectoryEntry> { sponsor.DirectoryEntry },
                DateDisplayOption.NotDisplayed,
                ItemDisplayType.Email,
                link2Name,
                link3Name).ToList();

            var vm = vms.First();

            // Force listing-page link & remove Tor/I2P
            vm.LinkType = LinkType.ListingPage;
            vm.Link2 = null;
            vm.Link2A = null;
            vm.Link3 = null;
            vm.Link3A = null;

            var htmlItem = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(vm, RootUrl);
            result.AppendLine(htmlItem);
        }
    }
}