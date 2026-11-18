using DirectoryManager.Data.Models;
using DirectoryManager.Web.Controllers;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    /// <summary>The AI review's suggested PGP key (found on the site) on approval.</summary>
    public class AiProposalPgpKeyTests
    {
        // LNVPS.net's real public key (https://lnvps.net/lnvps.asc), primary fingerprint below.
        private const string Key =
            "-----BEGIN PGP PUBLIC KEY BLOCK-----\n\n" +
            "mDMEZzz3SxYJKwYBBAHaRw8BAQdA+aAQWjWUIzS7gD2Cq83yQK+CJtWEuqv1cAFU\n" +
            "jwvLpRi0G0xOVlBTLm5ldCA8c2FsZXNAbG52cHMubmV0PoiZBBMWCgBBFiEEnQ44\n" +
            "TyK1cOyGQUo0BUVCibEtEd4FAmc890sCGwMFCQWjmoAFCwkIBwICIgIGFQoJCAsC\n" +
            "BBYCAwECHgcCF4AACgkQBUVCibEtEd45mAD9E0Iybxqt7Cb+nyC3Nu7QeL1zBy4G\n" +
            "iCJgB+RcLZDRQCEBANDIzM90nFN9ojmsfte8zH+v71tEcC8IokIL0Ehyn4gIuDgE\n" +
            "Zzz3SxIKKwYBBAGXVQEFAQEHQPGJhUgGvYn82mlGTfo080tHD9j5EyijsMpFImT4\n" +
            "argrAwEIB4h+BBgWCgAmFiEEnQ44TyK1cOyGQUo0BUVCibEtEd4FAmc890sCGwwF\n" +
            "CQWjmoAACgkQBUVCibEtEd7UkQD/cazecmF3JHQ+Vk8/3zozRNMNRO78gMKS+Ve8\n" +
            "+0Q/kpEBANEW3Z0501WNrGJf/jnlBf4VUH+XPA1i4nq9U4DLjD0G\n" +
            "=71iA\n" +
            "-----END PGP PUBLIC KEY BLOCK-----\n";

        private const string Fingerprint = "9D0E384F22B570EC86414A3405454289B12D11DE";

        private static Submission Reviewed(string? submittedKey, string? proposedKey) => new ()
        {
            Name = "LNVPS",
            Link = "https://lnvps.net/",
            ProposedName = "LNVPS",
            ReviewModel = "claude-sonnet-5-5",
            PgpKey = submittedKey,
            ProposedPgpKey = proposedKey,
            ProposedPgpKeySource = "https://lnvps.net/lnvps.asc",
        };

        [Fact] // HAPPY: no key submitted → the key found on the site is used
        public void FillsEmptyKey_WithSuggestedKey()
        {
            var s = Reviewed(null, Key);

            Assert.Equal(Fingerprint, SubmissionController.SuggestedPgpFingerprint(s));
            Assert.Equal(Key.Trim(), SubmissionController.WithProposal(s).PgpKey);
        }

        [Fact] // SAD: the submitter's own key is never replaced by one found on the site
        public void KeepsSubmittedKey()
        {
            var s = Reviewed("-----BEGIN PGP PUBLIC KEY BLOCK-----\n\nsubmitted\n-----END PGP PUBLIC KEY BLOCK-----", Key);

            Assert.Equal(s.PgpKey, SubmissionController.WithProposal(s).PgpKey);
        }

        [Fact] // SAD: a suggested block that doesn't parse as a key is ignored
        public void IgnoresUnreadableSuggestedKey()
        {
            var s = Reviewed(null, "-----BEGIN PGP PUBLIC KEY BLOCK-----\n\nAAAA\n-----END PGP PUBLIC KEY BLOCK-----");

            Assert.Null(SubmissionController.SuggestedPgpFingerprint(s));
            Assert.Null(SubmissionController.WithProposal(s).PgpKey);
        }
    }
}
