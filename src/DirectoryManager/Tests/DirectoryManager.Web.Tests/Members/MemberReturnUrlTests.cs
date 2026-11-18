using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Members
{
    public class MemberReturnUrlTests
    {
        [Theory]
        [InlineData("/members/reviews/write/some-listing", "/members/reviews/write/some-listing")]
        [InlineData("/Members/Reviews/Write/Some-Listing", "/members/reviews/write/some-listing")]
        [InlineData("/members/replies/new/42", "/members/replies/new/42")]
        [InlineData("/members/reviews", "/members/reviews")]
        [InlineData("/members/account", "/members/account")]
        public void MemberPagesAreAllowed(string input, string expected) => Assert.Equal(expected, MemberReturnUrl.Safe(input));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("/account/home")]
        [InlineData("/admin/reviews")]
        [InlineData("/account/two-factor")]
        [InlineData("/submission/1/review")]
        [InlineData("https://evil.example/members/account")]
        [InlineData("//evil.example/members/account")]
        [InlineData("/\\evil.example")]
        [InlineData("/members/reviews/write/../../account/home")]
        [InlineData("/members/reviews/write/%2e%2e/admin")]
        [InlineData("/members/reviews/write/x?next=/admin")]
        [InlineData("/members/reviews/write/")]
        [InlineData("/members/reviews/write/a b")]
        [InlineData("members/account")]
        public void EverythingElseIsDropped(string? input) => Assert.Null(MemberReturnUrl.Safe(input));
    }
}
