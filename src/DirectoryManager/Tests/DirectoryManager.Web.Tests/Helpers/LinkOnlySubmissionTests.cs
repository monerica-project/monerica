using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Models;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class LinkOnlySubmissionTests
    {
        [Theory]
        [InlineData("https://example.com", "https://example.com/", null)]
        [InlineData("https://example.com/", "https://example.com/", null)]
        [InlineData("example.com", "https://example.com/", null)]
        [InlineData("  https://www.example.com/pay/monero  ", "https://www.example.com/", "https://www.example.com/pay/monero")]
        [InlineData("https://example.com/cards?coin=xmr#top", "https://example.com/", "https://example.com/cards?coin=xmr")]
        public void PastedAddress_BecomesSiteLink_AndADeepPageBecomesTheProof(string pasted, string link, string? proof)
        {
            Assert.True(LinkOnlySubmission.TryParse(pasted, out var l, out var p, out _));
            Assert.Equal(link, l);
            Assert.Equal(proof, p);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a link")]
        [InlineData("localhost")]
        [InlineData("ftp://example.com/file")]
        [InlineData("javascript:alert(1)")]
        public void NotAWebLink_IsRefusedWithAReason(string pasted)
        {
            Assert.False(LinkOnlySubmission.TryParse(pasted, out _, out _, out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void PlaceholderName_IsTheHostWithoutWww()
        {
            Assert.Equal("example.com", LinkOnlySubmission.PlaceholderName("https://www.example.com/"));
            Assert.Equal("shop.example.org", LinkOnlySubmission.PlaceholderName("https://shop.example.org/"));
        }

        [Fact]
        public void LinkOnly_IsRecognisedByItsNote()
        {
            Assert.True(LinkOnlySubmission.IsLinkOnly(new Submission { NoteToAdmin = LinkOnlySubmission.NoteText + " Owner's note: sells coffee" }));
            Assert.False(LinkOnlySubmission.IsLinkOnly(new Submission { NoteToAdmin = "Please list my site" }));
            Assert.False(LinkOnlySubmission.IsLinkOnly(new Submission()));
        }
    }
}
