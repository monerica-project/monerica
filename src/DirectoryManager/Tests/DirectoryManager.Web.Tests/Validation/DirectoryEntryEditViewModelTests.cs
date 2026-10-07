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
