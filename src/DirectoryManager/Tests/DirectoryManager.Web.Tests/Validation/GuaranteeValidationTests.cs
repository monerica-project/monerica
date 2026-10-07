using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// Deposit-guarantee rows on the submit/edit/review forms: each row is optional, but a row is
    /// a PAIR — a proof link requires an amount and vice-versa; empty rows are ignored; links must
    /// be valid URLs. Happy + sad paths.
    /// </summary>
    public class GuaranteeValidationTests
    {
        private static ModelStateDictionary Validate(params GuaranteeItem[] items)
        {
            var ms = new ModelStateDictionary();
            GuaranteeValidation.ValidateGuarantees(items, ms);
            return ms;
        }

        [Fact] // HAPPY: a complete link+amount pair is valid.
        public void CompletePair_IsValid()
        {
            var ms = Validate(new GuaranteeItem { Link = "https://bitcointalk.org/thread", Amount = 1000m });
            Assert.True(ms.IsValid);
        }

        [Fact] // HAPPY: blank rows are ignored (guarantees are optional).
        public void EmptyRows_AreIgnored()
        {
            var ms = Validate(new GuaranteeItem(), new GuaranteeItem { Link = "   ", Amount = null });
            Assert.True(ms.IsValid);
        }

        [Fact] // SAD: a link with no amount is a half-filled pair.
        public void LinkWithoutAmount_IsInvalid()
        {
            var ms = Validate(new GuaranteeItem { Link = "https://bitcointalk.org/thread", Amount = null });
            Assert.False(ms.IsValid);
            Assert.True(ms.ContainsKey("Guarantees[0].Amount"));
        }

        [Fact] // SAD: an amount with no link is a half-filled pair.
        public void AmountWithoutLink_IsInvalid()
        {
            var ms = Validate(new GuaranteeItem { Link = null, Amount = 500m });
            Assert.False(ms.IsValid);
            Assert.True(ms.ContainsKey("Guarantees[0].Link"));
        }

        [Fact] // SAD: a non-URL proof link is rejected.
        public void InvalidUrl_IsInvalid()
        {
            var ms = Validate(new GuaranteeItem { Link = "not a url", Amount = 500m });
            Assert.False(ms.IsValid);
            Assert.True(ms.ContainsKey("Guarantees[0].Link"));
        }

        [Fact] // SAD: amount <= 0 isn't a real amount, so a link with 0 needs an amount.
        public void ZeroAmount_WithLink_IsInvalid()
        {
            var ms = Validate(new GuaranteeItem { Link = "https://x.io", Amount = 0m });
            Assert.False(ms.IsValid);
            Assert.True(ms.ContainsKey("Guarantees[0].Amount"));
        }

        [Fact] // The second row's errors are keyed by its own index.
        public void SecondRowError_IsKeyedByIndex()
        {
            var ms = Validate(
                new GuaranteeItem { Link = "https://x.io", Amount = 100m }, // row 0 ok
                new GuaranteeItem { Link = "https://y.io", Amount = null }); // row 1 bad
            Assert.False(ms.IsValid);
            Assert.True(ms.ContainsKey("Guarantees[1].Amount"));
            Assert.False(ms.ContainsKey("Guarantees[0].Amount"));
        }
    }
}
