using DirectoryManager.Data.Enums;
using DirectoryManager.DisplayFormatting.Enums;
using DirectoryManager.DisplayFormatting.Helpers;
using DirectoryManager.DisplayFormatting.Models;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>
    /// Category list items: the purple Tor box next to the orange external-link box.
    /// </summary>
    public class TorLinkBoxTests
    {
        private const string Onion = "http://exampleonionaddressxyz.onion/";

        [Fact]
        public void ListingWithOnion_ShowsTorBoxAfterExternalBox()
        {
            var html = Render(Model(DirectoryStatus.Admitted, Onion));

            Assert.Contains("class=\"external-link tor-link\"", html);
            Assert.Contains($"href=\"{Onion}\"", html);
            Assert.True(
                html.IndexOf("class=\"external-link\"", StringComparison.Ordinal) <
                html.IndexOf("class=\"external-link tor-link\"", StringComparison.Ordinal));
        }

        [Fact]
        public void ScamListing_HasNoTorBoxAndNoExternalBox()
        {
            var html = Render(Model(DirectoryStatus.Scam, Onion));

            Assert.DoesNotContain("tor-link", html);
            Assert.DoesNotContain("class=\"external-link\"", html);
            Assert.DoesNotContain(".onion", html);
        }

        [Fact]
        public void ListingWithoutOnion_HasNoTorBox()
        {
            var html = Render(Model(DirectoryStatus.Verified, null));

            Assert.Contains("class=\"external-link\"", html);
            Assert.DoesNotContain("tor-link", html);
        }

        [Fact]
        public void QuestionableListing_TorBoxIsNofollow()
        {
            var html = Render(Model(DirectoryStatus.Questionable, Onion));

            Assert.Contains("rel=\"nofollow\" class=\"external-link tor-link\"", html);
        }

        [Fact]
        public void AffiliateOnion_IsUsedWhenNotSponsored()
        {
            var model = Model(DirectoryStatus.Admitted, Onion);
            model.Link2A = "http://affiliateonionaddress.onion/?ref=1";

            var html = Render(model);

            Assert.Contains("href=\"http://affiliateonionaddress.onion/?ref=1\"", html);
            Assert.Contains("rel=\"sponsored\" class=\"external-link tor-link\"", html);
        }

        private static string Render(DirectoryEntryViewModel model) =>
            DisplayMarkUpHelper.GenerateDirectoryEntryHtml(model, "https://dir.example");

        private static DirectoryEntryViewModel Model(DirectoryStatus status, string? link2) => new ()
        {
            DirectoryEntryId = 1,
            Name = "Example Swap",
            DirectoryEntryKey = "example-swap",
            Link = "https://example.com",
            Link2 = link2,
            Link2Name = "Tor",
            DirectoryStatus = status,
            LinkType = LinkType.ListingPage,
        };
    }
}
