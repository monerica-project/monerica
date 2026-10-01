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
        public static void ValidateGuarantees(IList<GuaranteeItem>? guarantees, ModelStateDictionary modelState)
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

                if (hasLink && !UrlHelper.IsValidUrl(g.Link!))
                {
                    modelState.AddModelError($"Guarantees[{i}].Link", $"Guarantee {i + 1}: the proof link is not a valid URL.");
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
