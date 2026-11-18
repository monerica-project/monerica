using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// The form checks for a listing's restrictions, shared by the admin listing form, the public
    /// submission forms and the admin submission review: every country entry must be a known
    /// country, the "other" line has a length cap, and Tor can't be "Not Allowed" on a listing with
    /// a .onion link.
    /// </summary>
    public static class RestrictionValidation
    {
        public const string RestrictedCountriesField = "RestrictedCountries";
        public const string TorPolicyField = "TorPolicy";
        public const string OtherRestrictionsField = "OtherRestrictions";

        /// <returns>The validation errors (empty when the input is fine).</returns>
        public static List<ValidationResult> Validate(string? restrictedCountries, TorPolicy torPolicy, string? otherRestrictions, params string?[] links)
        {
            var results = new List<ValidationResult>();

            var countriesError = ListingRestrictions.CountriesError(restrictedCountries);
            if (countriesError != null)
            {
                results.Add(new ValidationResult(countriesError, new[] { RestrictedCountriesField }));
            }

            var other = ListingRestrictions.CleanOther(otherRestrictions);
            if (other != null && other.Length > ListingRestrictions.OtherRestrictionsMaxLength)
            {
                results.Add(new ValidationResult(
                    $"Other restrictions is {other.Length} characters, which is over the {ListingRestrictions.OtherRestrictionsMaxLength}-character limit. Please shorten it.",
                    new[] { OtherRestrictionsField }));
            }

            var torError = ListingRestrictions.TorConflictError(torPolicy, links);
            if (torError != null)
            {
                results.Add(new ValidationResult(torError, new[] { TorPolicyField }));
            }

            return results;
        }

        /// <summary>Adds <see cref="Validate"/>'s errors to a ModelState (for actions that bind a data model directly).</summary>
        public static void AddTo(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string? restrictedCountries, TorPolicy torPolicy, string? otherRestrictions, params string?[] links)
        {
            foreach (var r in Validate(restrictedCountries, torPolicy, otherRestrictions, links))
            {
                foreach (var member in r.MemberNames)
                {
                    modelState.AddModelError(member, r.ErrorMessage ?? string.Empty);
                }
            }
        }
    }
}
