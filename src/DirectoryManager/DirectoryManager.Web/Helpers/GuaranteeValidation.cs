using DirectoryManager.Data.Models;
using DirectoryManager.Utilities.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Shared validation for the (up to 4) deposit-guarantee rows on the submit/edit/review forms.
    /// Every row is optional, but a row is a PAIR: a link requires an amount and an amount requires
    /// a link. Empty rows are ignored.
    /// </summary>
    public static class GuaranteeValidation
    {
        /// <param name="guarantees">The posted rows.</param>
        /// <param name="modelState">Receives the errors.</param>
        /// <param name="listingLinks">
        /// The listing's own links. A deposit guarantee is held by a THIRD PARTY (a forum, an escrow
        /// or guarantor site), so its proof link may not be on the listing's own domain (or a
        /// subdomain of it): a site vouching for itself is not a guarantee.
        /// </param>
        public static void ValidateGuarantees(IList<GuaranteeItem>? guarantees, ModelStateDictionary modelState, params string?[] listingLinks)
        {
            for (int i = 0; i < (guarantees?.Count ?? 0); i++)
            {
                var g = guarantees![i];
                var hasLink = !string.IsNullOrWhiteSpace(g.Link);
                var hasAmount = g.Amount.HasValue && g.Amount.Value > 0;

                if (!hasLink && !hasAmount)
                {
                    continue; // empty row — guarantees are optional
                }

                if (hasLink && g.Link!.Length > DirectoryManager.Data.Helpers.ListingText.LinkMaxLength)
                {
                    modelState.AddModelError($"Guarantees[{i}].Link", $"Guarantee {i + 1}: the proof link is longer than {DirectoryManager.Data.Helpers.ListingText.LinkMaxLength} characters.");
                }

                if (hasLink && !UrlHelper.IsValidUrl(g.Link!))
                {
                    modelState.AddModelError($"Guarantees[{i}].Link", $"Guarantee {i + 1}: the proof link is not a valid URL.");
                }

                if (hasLink && listingLinks != null)
                {
                    var own = listingLinks.FirstOrDefault(l => UrlHelper.HostsRelated(g.Link, l));
                    if (own != null)
                    {
                        modelState.AddModelError(
                            $"Guarantees[{i}].Link",
                            $"Guarantee {i + 1}: the proof link is on the listing's own site ({UrlHelper.TryGetHost(own)}). " +
                            "A deposit guarantee must be shown on a third-party site (for example a forum or a guarantor), not on the listing's own domain.");
                    }
                }

                if (hasLink && !hasAmount)
                {
                    modelState.AddModelError($"Guarantees[{i}].Amount", $"Guarantee {i + 1}: enter a deposit amount (a link needs an amount).");
                }

                if (hasAmount && !hasLink)
                {
                    modelState.AddModelError($"Guarantees[{i}].Link", $"Guarantee {i + 1}: enter a proof link (an amount needs a link).");
                }
            }
        }
    }
}
