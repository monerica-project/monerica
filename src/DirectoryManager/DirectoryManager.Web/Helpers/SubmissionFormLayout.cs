using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Layout of the public submission form (Views/Shared/_SubmissionForm.cshtml, used by
    /// Submission/Create and Submission/SubmitEdit): the required fields at the top, then the optional
    /// fields in collapsible groups. A group renders open when any of its fields has a value or a
    /// validation error, so nothing the visitor typed (and no error) is ever hidden. No JavaScript.
    /// Layout only: field names, binding and validation live on <see cref="SubmissionRequest"/> and
    /// the controller.
    /// </summary>
    public static class SubmissionFormLayout
    {
        /// <summary>
        /// The fields a submission can't go ahead without: Name, Link and Description are [Required]
        /// on the model; the subcategory is enforced at the confirm step ("select a subcategory or
        /// supply a suggested one").
        /// </summary>
        public static readonly IReadOnlyList<string> RequiredFields = new[]
        {
            nameof(SubmissionRequest.SubCategoryId),
            nameof(SubmissionRequest.Name),
            nameof(SubmissionRequest.Link),
            nameof(SubmissionRequest.Description),
        };

        public static readonly FormGroup MoreLinks = new (
            "more-links",
            new[]
            {
                nameof(SubmissionRequest.Link2), nameof(SubmissionRequest.Link3), nameof(SubmissionRequest.ProofLink),
                nameof(SubmissionRequest.VideoLink), nameof(SubmissionRequest.SourceCodeLink),
                nameof(SubmissionRequest.RelatedLink1), nameof(SubmissionRequest.RelatedLink2), nameof(SubmissionRequest.RelatedLink3),
            },
            m => Any(m.Link2, m.Link3, m.ProofLink, m.VideoLink, m.SourceCodeLink, m.RelatedLink1, m.RelatedLink2, m.RelatedLink3));

        public static readonly FormGroup Contact = new (
            "contact",
            new[] { nameof(SubmissionRequest.Email), nameof(SubmissionRequest.Messenger), nameof(SubmissionRequest.Social), nameof(SubmissionRequest.PgpKey) },
            m => Any(m.Email, m.Messenger, m.Social, m.PgpKey));

        public static readonly FormGroup Location = new (
            "location",
            new[]
            {
                nameof(SubmissionRequest.CountryCode), nameof(SubmissionRequest.StreetAddress), nameof(SubmissionRequest.City),
                nameof(SubmissionRequest.StateRegion), nameof(SubmissionRequest.PostalCode),
            },
            m => Any(m.CountryCode) || PostalAddress.HasAny(m.StreetAddress, m.City, m.StateRegion, m.PostalCode));

        public static readonly FormGroup Business = new (
            "business",
            new[]
            {
                nameof(SubmissionRequest.Processor), nameof(SubmissionRequest.FoundedYear), nameof(SubmissionRequest.FoundedMonth),
                nameof(SubmissionRequest.FoundedDay), nameof(SubmissionRequest.Note),
            },
            m => Any(m.Processor, m.FoundedYear, m.FoundedMonth, m.FoundedDay, m.Note));

        public static readonly FormGroup KycAndJavaScript = new (
            "kyc-javascript",
            new[] { nameof(SubmissionRequest.KycPolicy), nameof(SubmissionRequest.SiteJavaScript) },
            m => (m.KycPolicy != null && m.KycPolicy != KycPolicy.Unknown && m.KycPolicy != KycPolicy.NotStated)
                || m.SiteJavaScript != SiteJavaScript.Unknown);

        /// <summary>
        /// Rendered by the shared Views/Shared/_RestrictionFields.cshtml partial, which works out its
        /// own open state with the same rule (it is also used by admin forms with other models).
        /// </summary>
        public static readonly FormGroup Restrictions = new (
            "restrictions",
            new[]
            {
                nameof(SubmissionRequest.RestrictedCountries), nameof(SubmissionRequest.VpnPolicy),
                nameof(SubmissionRequest.TorPolicy), nameof(SubmissionRequest.OtherRestrictions),
            },
            m => Any(m.RestrictedCountries, m.OtherRestrictions) || m.VpnPolicy != VpnPolicy.Unknown || m.TorPolicy != TorPolicy.Unknown);

        public static readonly FormGroup Exchanges = new (
            "exchanges",
            new[] { nameof(SubmissionRequest.Liquidity), nameof(SubmissionRequest.RateType), nameof(SubmissionRequest.Guarantees) },
            m => m.Liquidity.IsPubliclyShown()
                || m.RateType.IsShown()
                || (m.Guarantees?.Any(g => !string.IsNullOrWhiteSpace(g?.Link) || (g?.Amount ?? 0) > 0) ?? false));

        public static readonly FormGroup CategoriesAndTags = new (
            "categories-tags",
            new[] { nameof(SubmissionRequest.SelectedTagIds), nameof(SubmissionRequest.Tags), nameof(SubmissionRequest.SuggestedSubCategory) },
            m => (m.SelectedTagIds?.Any(id => id > 0) ?? false) || Any(m.Tags, m.SuggestedSubCategory));

        public static readonly FormGroup NoteToReviewer = new (
            "note-to-reviewer",
            new[] { nameof(SubmissionRequest.NoteToAdmin) },
            m => Any(m.NoteToAdmin));

        /// <summary>The optional groups in the order they appear on the form.</summary>
        public static readonly IReadOnlyList<FormGroup> OptionalGroups = new[]
        {
            MoreLinks, Contact, Location, Business, KycAndJavaScript, Restrictions, Exchanges, CategoriesAndTags, NoteToReviewer,
        };

        /// <summary>
        /// Properties of <see cref="SubmissionRequest"/> that are not visible form fields (hidden ids,
        /// display-only status, and the CSV the controller builds from the tag checkboxes).
        /// </summary>
        public static readonly IReadOnlyList<string> NonFormProperties = new[]
        {
            nameof(SubmissionRequest.SubmissionId),
            nameof(SubmissionRequest.DirectoryEntryId),
            nameof(SubmissionRequest.DirectoryStatus),
            nameof(SubmissionRequest.CurrentDirectoryStatus),
            nameof(SubmissionRequest.SelectedTagIdsCsv),
        };

        /// <summary>
        /// True when the group should render open: one of its fields has a value, or a validation
        /// error (including indexed keys such as "Guarantees[1].Amount").
        /// </summary>
        /// <param name="group">The form group.</param>
        /// <param name="model">The posted or loaded model.</param>
        /// <param name="modelState">The current model state (errors).</param>
        /// <returns>Whether to add the <c>open</c> attribute.</returns>
        public static bool IsOpen(FormGroup group, SubmissionRequest model, ModelStateDictionary? modelState)
            => group.HasValue(model) || HasError(modelState, group.Fields);

        /// <summary>True when any of the fields (or an indexed/child key under it) has an error.</summary>
        /// <param name="modelState">The model state.</param>
        /// <param name="fields">Field names.</param>
        /// <returns>Whether an error exists.</returns>
        public static bool HasError(ModelStateDictionary? modelState, IEnumerable<string> fields)
        {
            if (modelState == null || modelState.ErrorCount == 0)
            {
                return false;
            }

            foreach (var entry in modelState)
            {
                if (entry.Value.Errors.Count == 0 || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                foreach (var field in fields)
                {
                    if (string.Equals(entry.Key, field, StringComparison.OrdinalIgnoreCase)
                        || entry.Key.StartsWith(field + "[", StringComparison.OrdinalIgnoreCase)
                        || entry.Key.StartsWith(field + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The " open" attribute text for a details element, or empty.</summary>
        /// <param name="group">The form group.</param>
        /// <param name="model">The model.</param>
        /// <param name="modelState">The model state.</param>
        /// <returns>" open" or an empty string.</returns>
        public static string OpenAttribute(FormGroup group, SubmissionRequest model, ModelStateDictionary? modelState)
            => IsOpen(group, model, modelState) ? " open" : string.Empty;

        /// <summary>Plain-language meaning of a KYC option on the form (same wording as the filter tooltips).</summary>
        /// <param name="policy">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(KycPolicy policy) => policy switch
        {
            KycPolicy.NotStated or KycPolicy.Unknown => "The site hasn't published a KYC policy, or you don't know it.",
            _ => FilterOptionTooltips.For(policy),
        };

        /// <summary>Plain-language meaning of a JavaScript option on the form.</summary>
        /// <param name="js">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(SiteJavaScript js) => js switch
        {
            SiteJavaScript.Unknown => "You don't know whether the website needs JavaScript.",
            _ => js.Explanation(),
        };

        /// <summary>Plain-language meaning of a liquidity option on the form (same wording as the filter tooltips).</summary>
        /// <param name="liquidity">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(Liquidity liquidity) => liquidity switch
        {
            Liquidity.NotApplicable or Liquidity.Unknown => "Not an exchange or swap service, or you don't know. Nothing is shown on the listing.",
            _ => FilterOptionTooltips.For(liquidity),
        };

        /// <summary>Plain-language meaning of a rate type option (same wording as the listing page's hover text).</summary>
        /// <param name="rateType">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(RateType rateType) => rateType switch
        {
            RateType.Fixed => "The rate is locked when you start the swap.",
            RateType.Float => "The rate follows the market until your deposit arrives.",
            RateType.FixedAndFloat => "You can choose either a fixed or a float rate.",
            _ => "Not an exchange or swap service, or you don't know. Nothing is shown on the listing.",
        };

        /// <summary>Plain-language meaning of a VPN option.</summary>
        /// <param name="policy">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(VpnPolicy policy) => policy switch
        {
            VpnPolicy.Allowed => "The service can be used over a VPN.",
            VpnPolicy.NotAllowed => "The service can't be used over a VPN.",
            VpnPolicy.MayTriggerVerification => "It works, but a VPN connection may get the account held for extra checks.",
            _ => "You don't know. Nothing is shown on the listing.",
        };

        /// <summary>Plain-language meaning of a Tor option.</summary>
        /// <param name="policy">The option.</param>
        /// <returns>The explanation.</returns>
        public static string OptionHelp(TorPolicy policy) => policy switch
        {
            TorPolicy.Allowed => "The service can be used over Tor.",
            TorPolicy.NotAllowed => "The service can't be used over Tor. Can't be chosen for a listing with a .onion link.",
            TorPolicy.MayTriggerVerification => "It works, but a Tor connection may get the account held for extra checks.",
            _ => "You don't know. Nothing is shown on the listing.",
        };

        private static bool Any(params string?[] values) => values.Any(v => !string.IsNullOrWhiteSpace(v));

        /// <summary>One collapsible optional group on the submission form.</summary>
        /// <param name="Id">Stable id (used for the details element and help ids).</param>
        /// <param name="Fields">Model property names shown in the group.</param>
        /// <param name="HasValue">True when the model has something filled in for the group.</param>
        public sealed record FormGroup(string Id, IReadOnlyList<string> Fields, Func<SubmissionRequest, bool> HasValue);
    }
}
