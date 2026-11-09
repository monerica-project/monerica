using DirectoryManager.Data.Enums;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Models;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SiteJavaScriptTests
    {
        [Theory]
        [InlineData(SiteJavaScript.Required, true)]
        [InlineData(SiteJavaScript.Optional, true)]
        [InlineData(SiteJavaScript.NotRequired, true)]
        [InlineData(SiteJavaScript.None, true)]
        [InlineData(SiteJavaScript.Unknown, false)]
        public void OnlyRealAnswers_AreShown(SiteJavaScript value, bool shown) => Assert.Equal(shown, value.IsShown());

        [Fact]
        public void NewSubmission_DefaultsToNotStated()
        {
            Assert.Equal(SiteJavaScript.Unknown, new SubmissionRequest().SiteJavaScript);
            Assert.Equal(SiteJavaScript.Unknown, new DirectoryEntryEditViewModel().SiteJavaScript);
        }

        [Fact]
        public void Dropdown_IsNotStatedThenTheThreeAnswers()
        {
            Assert.Equal(
                new[] { "Not Stated", "Required", "Optional", "None" },
                SiteJavaScriptExtensions.FormOrder.Select(v => EnumHelper.GetDescription(v)).ToArray());
        }

        [Fact]
        public void RetiredNotRequired_ShowsAndFiltersAsOptional()
        {
            Assert.Equal(SiteJavaScript.Optional, SiteJavaScript.NotRequired.Normalize());
            Assert.Equal("Optional", EnumHelper.GetDescription(SiteJavaScript.NotRequired));
            Assert.DoesNotContain(SiteJavaScript.NotRequired, SiteJavaScriptExtensions.FormOrder);
            Assert.DoesNotContain(SiteJavaScript.NotRequired, SiteJavaScriptExtensions.Filterable);
        }

        [Theory]
        [InlineData(SiteJavaScript.None)]
        [InlineData(SiteJavaScript.Optional)]
        [InlineData(SiteJavaScript.Required)]
        public void EveryOption_HasAnExplanation(SiteJavaScript value) => Assert.False(string.IsNullOrWhiteSpace(value.Explanation()));

        [Fact]
        public void Filter_IncludesNotStated_SoAllTickedShowsEverything() =>
            Assert.Equal(
                new[] { SiteJavaScript.None, SiteJavaScript.Optional, SiteJavaScript.Required, SiteJavaScript.Unknown },
                SiteJavaScriptExtensions.Filterable);
    }
}
