using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class SocialLinkScanTests
    {
        [Fact] // HAPPY: OKSPOT-style links that only exist in the JavaScript bundle
        public void Finds_ProfilesInScriptBundle()
        {
            var js = "const f=[{href:\"https://t.me/okspot_com\"},{href:\"https://x.com/okspot_com\"}];";
            var (social, messenger) = SocialLinkScan.Find("<html></html>", js);
            Assert.Equal(new[] { "https://x.com/okspot_com" }, social);
            Assert.Equal(new[] { "https://t.me/okspot_com" }, messenger);
        }

        [Fact] // HAPPY: twitter.com becomes x.com, a post URL is trimmed to the profile
        public void Normalizes_TwitterPostToProfile()
        {
            var (social, _) = SocialLinkScan.Find("<a href=\"https://twitter.com/acme/status/12345\">us</a>");
            Assert.Equal(new[] { "https://x.com/acme" }, social);
        }

        [Theory] // SAD: share buttons and platform pages are not the business's profile
        [InlineData("https://twitter.com/intent/tweet?text=hi")]
        [InlineData("https://x.com/share?url=https://acme.com")]
        [InlineData("https://t.me/share/url?url=https://acme.com")]
        [InlineData("https://www.facebook.com/sharer/sharer.php?u=acme")]
        [InlineData("https://x.com/privacy")]
        public void Ignores_ShareAndFeatureLinks(string url)
        {
            var (social, messenger) = SocialLinkScan.Find($"<a href=\"{url}\">share</a>");
            Assert.Empty(social);
            Assert.Empty(messenger);
        }

        [Fact]
        public void DiscordInvite_IsMessenger()
        {
            var (_, messenger) = SocialLinkScan.Find("https://discord.com/invite/abc123 and https://discord.gg/xyz");
            Assert.Equal(new[] { "https://discord.com/invite/abc123", "https://discord.gg/xyz" }, messenger);
        }

        [Fact] // SAD: submitter put the website in Social and the AI kept it → cleared
        public void Mapper_ClearsSocialThatRepeatsTheMainLink()
        {
            var s = new Submission { SubmissionId = 1, Name = "OKSPOT", Link = "https://okspot.com", Social = "https://okspot.com/" };
            var v = new AiVerdict { Verdict = "approve_candidate", Proposal = new AiProposal { Social = null, Messenger = "https://t.me/okspot_com" } };
            ProposalMapper.Apply(s, v, new Dictionary<string, int>());
            Assert.Equal(string.Empty, s.ProposedSocial);
            Assert.Equal("https://t.me/okspot_com", s.ProposedMessenger);
        }

        [Fact] // HAPPY: AI replaces the bad value with the real profile
        public void Mapper_UsesProposedProfile()
        {
            var s = new Submission { SubmissionId = 1, Name = "OKSPOT", Link = "https://okspot.com", Social = "https://okspot.com" };
            var v = new AiVerdict { Verdict = "approve_candidate", Proposal = new AiProposal { Social = "https://x.com/okspot_com" } };
            ProposalMapper.Apply(s, v, new Dictionary<string, int>());
            Assert.Equal("https://x.com/okspot_com", s.ProposedSocial);
            Assert.Null(s.ProposedMessenger);
        }
    }
}
