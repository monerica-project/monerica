using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// Business-rule validation on the public submission model (IValidatableObject): clean copy
    /// is accepted; sentences can't end with "!"; a proof link must differ from the main link; a
    /// PGP key must be a real armored key. (XSS/HTML-guard rules are covered in
    /// SubmissionRequestXssTests.) Happy + sad paths.
    /// </summary>
    public class SubmissionRequestValidationTests
    {
        private static List<ValidationResult> Validate(SubmissionRequest m) =>
            m.Validate(new ValidationContext(m)).ToList();

        private static SubmissionRequest Clean() => new ()
        {
            Link = "https://example.com",
            Name = "Example Service",
            Description = "Accepts Monero for private hosting.",
        };

        private static bool HasErrorFor(IEnumerable<ValidationResult> results, string member) =>
            results.Any(r => r.MemberNames.Contains(member));

        [Fact] // HAPPY: a clean submission produces no business-rule errors.
        public void CleanSubmission_HasNoErrors()
        {
            Assert.Empty(Validate(Clean()));
        }

        [Fact] // SAD: a description sentence ending in "!" is rejected.
        public void DescriptionEndingInExclamation_IsRejected()
        {
            var m = Clean();
            m.Description = "This service is the best!";
            Assert.True(HasErrorFor(Validate(m), nameof(SubmissionRequest.Description)));
        }

        [Fact] // HAPPY: a "!" mid-word (e.g. a brand name) is allowed.
        public void ExclamationInsideName_IsAllowed()
        {
            var m = Clean();
            m.Description = "Works great with Yahoo!Mail and other providers.";
            Assert.False(HasErrorFor(Validate(m), nameof(SubmissionRequest.Description)));
        }

        [Fact] // SAD: proof link identical to the main link is rejected.
        public void ProofLinkSameAsMainLink_IsRejected()
        {
            var m = Clean();
            m.ProofLink = "https://example.com";
            Assert.True(HasErrorFor(Validate(m), nameof(SubmissionRequest.ProofLink)));
        }

        [Fact] // SAD: trivially-different form of the same URL is still caught (normalized compare).
        public void ProofLinkTrivialVariantOfMainLink_IsRejected()
        {
            var m = Clean();
            m.Link = "https://example.com";
            m.ProofLink = "http://www.example.com/";
            Assert.True(HasErrorFor(Validate(m), nameof(SubmissionRequest.ProofLink)));
        }

        [Fact] // HAPPY: a proof link pointing at a different page is allowed.
        public void ProofLinkDifferentPage_IsAllowed()
        {
            var m = Clean();
            m.ProofLink = "https://example.com/accepts-monero";
            Assert.False(HasErrorFor(Validate(m), nameof(SubmissionRequest.ProofLink)));
        }

        [Fact] // SAD: a non-PGP string in the PGP field is rejected.
        public void InvalidPgpKey_IsRejected()
        {
            var m = Clean();
            m.PgpKey = "this is not a pgp key";
            Assert.True(HasErrorFor(Validate(m), nameof(SubmissionRequest.PgpKey)));
        }
    }
}
