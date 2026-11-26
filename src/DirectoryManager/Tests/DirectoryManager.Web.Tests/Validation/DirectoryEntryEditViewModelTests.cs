using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// Business-rule validation on the admin listing-edit model (IValidatableObject): a clean edit
    /// is accepted; the Description/Note editorial length caps are enforced; a PGP key must be a
    /// real armored key. Mirrors the public submit rules so admin edit and submit stay in lockstep.
    /// Happy + sad paths.
    /// </summary>
    public class DirectoryEntryEditViewModelTests
    {
        private static List<ValidationResult> Validate(DirectoryEntryEditViewModel m) =>
            m.Validate(new ValidationContext(m)).ToList();

        private static DirectoryEntryEditViewModel Clean() => new ()
        {
            DirectoryEntryId = 1,
            DirectoryStatus = DirectoryStatus.Verified,
            SubCategoryId = 1,
            Name = "Example Service",
            Link = "https://example.com",
            Description = "Accepts Monero.",
        };

        private static bool HasErrorFor(IEnumerable<ValidationResult> results, string member) =>
            results.Any(r => r.MemberNames.Contains(member));

        [Fact] // HAPPY: a clean edit produces no business-rule errors.
        public void CleanEdit_HasNoErrors()
        {
            Assert.Empty(Validate(Clean()));
        }

        [Theory] // SAD: the main link must be a real web address.
        [InlineData("foldoy")]
        [InlineData("foldoy.com")]
        [InlineData("www.foldoy.com")]
        [InlineData("https://foldoy")]
        [InlineData("https://fold oy.com")]
        [InlineData("ftp://foldoy.com")]
        [InlineData("javascript:alert(1)")]
        [InlineData("not a link")]
        public void MainLinkThatIsNotALink_IsRejected(string link)
        {
            var m = Clean();
            m.Link = link;
            Assert.True(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.Link)));
        }

        [Theory] // HAPPY: normal, path, port and onion addresses are accepted.
        [InlineData("https://foldoy.com")]
        [InlineData("http://foldoy.com/")]
        [InlineData("  https://sub.foldoy.co.uk/path?x=1#y  ")]
        [InlineData("https://foldoy.com:8443/a")]
        [InlineData("http://utjpewosd5twy7qp7ckvyitlg5mc3h5kffkvngsv6yf65kuh425btrad.onion/")]
        public void MainLinkThatIsALink_IsAccepted(string link)
        {
            var m = Clean();
            m.Link = link;
            Assert.False(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.Link)));
        }

        [Fact] // SAD: the optional link fields get the same check; empty ones are fine.
        public void OptionalLinksThatAreNotLinks_AreRejected()
        {
            var m = Clean();
            m.Link2 = "foldoy";
            m.LinkA = "abc";
            m.ProofLink = "see homepage";
            m.Link3 = " ";
            var results = Validate(m);
            Assert.True(HasErrorFor(results, nameof(DirectoryEntryEditViewModel.Link2)));
            Assert.True(HasErrorFor(results, nameof(DirectoryEntryEditViewModel.LinkA)));
            Assert.True(HasErrorFor(results, nameof(DirectoryEntryEditViewModel.ProofLink)));
            Assert.False(HasErrorFor(results, nameof(DirectoryEntryEditViewModel.Link3)));
        }

        [Fact] // Boundary: a description exactly at the cap is allowed.
        public void DescriptionAtMaxLength_IsAllowed()
        {
            var m = Clean();
            m.Description = new string('x', DirectoryEntryEditViewModel.DescriptionMaxLength);
            Assert.False(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.Description)));
        }

        [Fact] // SAD: a description over the cap is rejected (not silently truncated).
        public void OverLengthDescription_IsRejected()
        {
            var m = Clean();
            m.Description = new string('x', DirectoryEntryEditViewModel.DescriptionMaxLength + 1);
            Assert.True(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.Description)));
        }

        [Fact] // SAD: a note over the cap is rejected.
        public void OverLengthNote_IsRejected()
        {
            var m = Clean();
            m.Note = new string('x', DirectoryEntryEditViewModel.NoteMaxLength + 1);
            Assert.True(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.Note)));
        }

        [Fact] // SAD: a non-PGP string in the PGP field is rejected.
        public void InvalidPgpKey_IsRejected()
        {
            var m = Clean();
            m.PgpKey = "this is not a pgp key";
            Assert.True(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.PgpKey)));
        }

        [Fact] // HAPPY: leaving the PGP field blank is fine.
        public void BlankPgpKey_IsAllowed()
        {
            var m = Clean();
            m.PgpKey = null;
            Assert.False(HasErrorFor(Validate(m), nameof(DirectoryEntryEditViewModel.PgpKey)));
        }
    }
}
