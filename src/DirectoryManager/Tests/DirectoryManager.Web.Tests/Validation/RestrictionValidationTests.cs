using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.DisplayFormatting.Models;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// The listing restrictions (countries not served, VPN, Tor, other) on the admin listing form
    /// and the public submission form: unknown countries are rejected with a clear message, the
    /// "other" line is capped, Tor can't be "Not Allowed" next to a .onion link, and the profile
    /// view model's blurb respects the onion link. Happy + sad paths.
    /// </summary>
    public class RestrictionValidationTests
    {
        private static DirectoryEntryEditViewModel CleanEdit() => new ()
        {
            DirectoryEntryId = 1,
            DirectoryStatus = DirectoryStatus.Verified,
            SubCategoryId = 1,
            Name = "Example Service",
            Link = "https://example.com",
            Description = "Accepts Monero.",
        };

        private static List<ValidationResult> Validate(IValidatableObject m) =>
            m.Validate(new ValidationContext(m)).ToList();

        private static bool HasErrorFor(IEnumerable<ValidationResult> results, string member) =>
            results.Any(r => r.MemberNames.Contains(member));

        [Fact] // HAPPY: codes and names, VPN/Tor answers and a short "other" line are accepted.
        public void ValidRestrictions_HaveNoErrors()
        {
            var m = CleanEdit();
            m.RestrictedCountries = "US, Iran, north korea";
            m.VpnPolicy = VpnPolicy.NotAllowed;
            m.TorPolicy = TorPolicy.Allowed;
            m.OtherRestrictions = "Minimum age 18";
            Assert.Empty(Validate(m));
        }

        [Fact] // SAD: an unknown country is rejected on the country field, naming the bad entry.
        public void UnknownCountry_IsRejected()
        {
            var m = CleanEdit();
            m.RestrictedCountries = "US, Atlantis";
            var results = Validate(m);
            Assert.True(HasErrorFor(results, nameof(m.RestrictedCountries)));
            Assert.Contains(results, r => r.ErrorMessage!.Contains("Atlantis"));
        }

        [Fact] // SAD: "other restrictions" over the cap.
        public void LongOtherRestrictions_IsRejected()
        {
            var m = CleanEdit();
            m.OtherRestrictions = new string('a', DirectoryManager.Data.Helpers.ListingRestrictions.OtherRestrictionsMaxLength + 1);
            Assert.True(HasErrorFor(Validate(m), nameof(m.OtherRestrictions)));
        }

        [Fact] // SAD: Tor "Not Allowed" contradicts the listing's own .onion link.
        public void TorNotAllowed_WithOnionLink_IsRejected()
        {
            var m = CleanEdit();
            m.Link2 = "http://exampleonionaddressxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.onion";
            m.TorPolicy = TorPolicy.NotAllowed;
            Assert.True(HasErrorFor(Validate(m), nameof(m.TorPolicy)));

            m.TorPolicy = TorPolicy.Allowed;
            Assert.False(HasErrorFor(Validate(m), nameof(m.TorPolicy)));
        }

        [Fact] // The public form runs the same checks.
        public void SubmissionRequest_RunsTheSameChecks()
        {
            var r = new SubmissionRequest
            {
                Name = "Example",
                Link = "https://example.com",
                Link3 = "http://abc.onion",
                Description = "Accepts Monero.",
                RestrictedCountries = "Mordor",
                TorPolicy = TorPolicy.NotAllowed,
            };
            var results = Validate(r);
            Assert.True(HasErrorFor(results, nameof(r.RestrictedCountries)));
            Assert.True(HasErrorFor(results, nameof(r.TorPolicy)));
        }

        [Fact] // New forms default to "Not Stated" for both.
        public void Defaults_AreNotStated()
        {
            Assert.Equal(VpnPolicy.Unknown, new SubmissionRequest().VpnPolicy);
            Assert.Equal(TorPolicy.Unknown, new SubmissionRequest().TorPolicy);
            Assert.Equal(VpnPolicy.Unknown, new DirectoryEntryEditViewModel().VpnPolicy);
            Assert.Equal(TorPolicy.Unknown, new DirectoryEntryEditViewModel().TorPolicy);
        }

        [Fact] // Profile: nothing recorded → no blurb; Tor "Not Allowed" never shows next to a .onion link.
        public void ProfileViewModel_Restrictions()
        {
            Assert.True(new DirectoryEntryViewModel { Link = "https://a.com" }.Restrictions.IsEmpty);

            var withOnion = new DirectoryEntryViewModel
            {
                Link = "https://a.com",
                Link2 = "http://abc.onion",
                TorPolicy = TorPolicy.NotAllowed,
                VpnPolicy = VpnPolicy.NotAllowed,
            };
            Assert.Equal("VPN not allowed", withOnion.Restrictions.SummaryLine);
        }

        [Fact] // Admin review diff lists a restriction change (country order/case differences are not a change).
        public void ComparisonDiff_ShowsRestrictionChanges()
        {
            var entry = new DirectoryEntry { Name = "A", DirectoryEntryKey = "a", Link = "https://a.com", RestrictedCountries = "IR,US" };
            var same = new Submission { Name = "A", Link = "https://a.com", RestrictedCountries = "us, ir" };
            var changed = new Submission { Name = "A", Link = "https://a.com", RestrictedCountries = "IR", VpnPolicy = VpnPolicy.NotAllowed };

            var noDiff = ModelComparisonHelpers.CompareEntries(entry, same);
            Assert.DoesNotContain("Countries not served", noDiff);

            var diff = ModelComparisonHelpers.CompareEntries(entry, changed);
            Assert.Contains("Countries not served", diff);
            Assert.Contains("VPN", diff);
        }
    }
}
