using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class AuthorDisplayTests
    {
        [Theory]
        [InlineData("7C2254208737705B341ECD070FFDCA4260FB1A25", "7C22…1A25")]
        [InlineData("  ABCDEF0123456789  ", "ABCD…6789")]
        [InlineData("SHORTNAME", "SHORTNAME")]
        [InlineData(null, "")]
        public void ShortFingerprint_KeepsFirstAndLastFour(string? input, string expected) =>
            Assert.Equal(expected, AuthorDisplay.ShortFingerprint(input));
    }
}
