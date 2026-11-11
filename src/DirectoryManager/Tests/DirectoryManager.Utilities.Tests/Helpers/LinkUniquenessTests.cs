using DirectoryManager.Utilities.Helpers;
using Xunit;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    public class LinkUniquenessTests
    {
        private static IReadOnlyList<(string Field, string Message)> Check(params (string, string?)[] fields) =>
            LinkUniqueness.FindDuplicates(fields.Select(f => (f.Item1, f.Item1, f.Item2)));

        [Fact] // SAD: listing 2376 had its website in Social too
        public void SocialSameAsMainLink_IsFlaggedOnSocial()
        {
            var errors = Check(("Link", "https://okspot.com"), ("Social", "okspot.com/"));
            Assert.Single(errors);
            Assert.Equal("Social", errors[0].Field);
        }

        [Fact] // HAPPY: the one allowed repeat
        public void ProofLinkSameAsMainLink_IsAllowed() =>
            Assert.Empty(Check(("Link", "https://acme.com"), ("ProofLink", "http://www.acme.com/")));

        [Fact] // SAD: proof link repeating any OTHER link is not allowed
        public void ProofLinkSameAsVideo_IsFlagged()
        {
            var errors = Check(("Link", "https://acme.com"), ("VideoLink", "https://youtu.be/x"), ("ProofLink", "https://youtu.be/x"));
            Assert.Single(errors);
            Assert.Equal("ProofLink", errors[0].Field);
        }

        [Fact] // SAD: same Tor link in Link 2 and Link 3
        public void SameOnionTwice_IsFlagged() =>
            Assert.Single(Check(("Link", "https://acme.com"), ("Link2", "http://abc.onion"), ("Link3", "http://abc.onion/")));

        [Fact] // HAPPY: different pages on the same site are different links
        public void DifferentPaths_AreFine() =>
            Assert.Empty(Check(("Link", "https://acme.com"), ("ProofLink", "https://acme.com/pay"), ("SourceCodeLink", "https://github.com/acme")));

        [Theory] // HAPPY: non-URL contact values are never compared
        [InlineData("@acme")]
        [InlineData("support@acme.com")]
        [InlineData("acme on telegram")]
        public void NonUrlContacts_AreIgnored(string value) =>
            Assert.Empty(Check(("Social", value), ("Messenger", value)));
    }
}
