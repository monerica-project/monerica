using DirectoryManager.Data.Enums;
using DirectoryManager.Web.Models;
using Xunit;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class RateTypeTests
    {
        [Theory]
        [InlineData(RateType.Fixed, true)]
        [InlineData(RateType.Float, true)]
        [InlineData(RateType.FixedAndFloat, true)]
        [InlineData(RateType.Unknown, false)]
        [InlineData(RateType.NotApplicable, false)]
        public void OnlyRealRates_AreShown(RateType rateType, bool shown) => Assert.Equal(shown, rateType.IsShown());

        [Fact]
        public void NewSubmission_DefaultsToNotApplicable() => Assert.Equal(RateType.NotApplicable, new SubmissionRequest().RateType);

        [Fact]
        public void NotApplicable_ReadsTheSameEverywhere()
        {
            // Every enum with a "not applicable" value uses the same label.
            Assert.Equal("Not Applicable", DirectoryManager.Utilities.Helpers.EnumHelper.GetDescription(RateType.NotApplicable));
            Assert.Equal("Not Applicable", DirectoryManager.Utilities.Helpers.EnumHelper.GetDescription(Liquidity.NotApplicable));
        }

        [Fact]
        public void PublicForm_NeverOffersUnknown()
        {
            Assert.DoesNotContain(RateType.Unknown, RateTypeExtensions.PublicFormOrder);
            Assert.Equal(RateType.NotApplicable, RateTypeExtensions.PublicFormOrder[0]);
        }

        [Fact]
        public void FormOrder_HasEveryValueOnce_NotApplicableFirst()
        {
            Assert.Equal(RateType.NotApplicable, RateTypeExtensions.FormOrder[0]);
            Assert.Equal(Enum.GetValues<RateType>().OrderBy(x => x), RateTypeExtensions.FormOrder.OrderBy(x => x));
        }
    }
}
