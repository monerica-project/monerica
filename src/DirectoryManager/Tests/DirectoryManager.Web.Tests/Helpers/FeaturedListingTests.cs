using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>
    /// Unpaid admin-picked "Featured" listing in a category/subcategory sponsor spot.
    /// </summary>
    public class FeaturedListingTests
    {
        private const string Site = "https://example.com/";
        private const string Affiliate = "https://example.com/?ref=monerica";
        private const string Onion = "http://exampleonionaddressxyz.onion/";

        [Theory]
        [InlineData(DirectoryStatus.Admitted, true)]
        [InlineData(DirectoryStatus.Verified, true)]
        [InlineData(DirectoryStatus.Questionable, false)]
        [InlineData(DirectoryStatus.Scam, false)]
        [InlineData(DirectoryStatus.Removed, false)]
        [InlineData(DirectoryStatus.Unknown, false)]
        public void OnlyAdmittedOrVerified_CanBeFeatured(DirectoryStatus status, bool expected)
        {
            Assert.Equal(expected, FeaturedListingRules.IsFeaturable(status));
        }

        [Fact]
        public void Featured_KeepsAffiliateLink_MarkedSponsored()
        {
            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(Model(Affiliate, null));

            Assert.Contains($"href=\"{Affiliate}\"", html);
            Assert.Contains("rel=\"sponsored", html);
        }

        [Fact]
        public void Featured_TorLink_IsNotMarkedSponsored()
        {
            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(Model(null, Onion));

            var torAnchorStart = html.IndexOf($"href=\"{Onion}\"", StringComparison.Ordinal);
            Assert.True(torAnchorStart > 0);
            var anchorOpen = html.LastIndexOf("<a ", torAnchorStart, StringComparison.Ordinal);
            var anchor = html.Substring(anchorOpen, torAnchorStart - anchorOpen);
            Assert.DoesNotContain("sponsored", anchor);
        }

        [Fact]
        public void Featured_RendersLikeSponsorBox_WithProfileLink()
        {
            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(Model(null, null));

            Assert.Contains("See profile", html);
            Assert.DoesNotContain("· An example listing.", html);
            Assert.Contains("An example listing.", html);
            Assert.Contains("Leave a review", html);
        }

        [Fact]
        public void PaidSponsor_StillUsesPlainLink_NotAffiliate()
        {
            var vm = Model(Affiliate, null);
            vm.IsFeatured = false;
            vm.IsSponsored = true;

            var html = DisplayMarkUpHelper.GenerateDirectoryEntryHtml(vm);

            Assert.Contains($"href=\"{Site}\"", html);
            Assert.DoesNotContain($"href=\"{Affiliate}\"", html);
        }

        private static DirectoryEntryViewModel Model(string? linkA, string? link2) => new ()
        {
            DirectoryEntryId = 1,
            DirectoryEntryKey = "example",
            Name = "Example",
            Description = "An example listing.",
            DirectoryStatus = DirectoryStatus.Verified,
            Link = Site,
            LinkA = linkA,
            Link2 = link2,
            Link2Name = "Tor",
            ItemPath = "/site/example",
            IsFeatured = true,
            SponsorInlineHeader = true,
            SkipLinksBlock = true,
            ShowProfileLink = true,
            LinkType = LinkType.Direct,
            DateOption = DateDisplayOption.NotDisplayed,
        };
    }
}
