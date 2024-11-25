using DirectoryManager.Utilities.Helpers;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    public class StringHelpersTests
    {
        [Theory]
        [InlineData("Crème Brûlée", "creme-brulee")]
        [InlineData("L'école est fermée", "l-ecole-est-fermee")]
        [InlineData("Café & Restaurant", "cafe-and-restaurant")]
        [InlineData("This%is#a test", "this-is-a-test")]
        [InlineData("  Spaces   and --- Symbols ", "spaces-and-symbols")]
        [InlineData("Mötörhead#%Concert", "motorhead-concert")]
        public void UrlKey_ReturnsCorrectUrlSafeString(string input, string expected)
        {
            // Act
            string result = StringHelpers.UrlKey(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void UrlKey_EmptyInput_ReturnsEmptyString()
        {
            // Act
            string result = StringHelpers.UrlKey(string.Empty);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void UrlKey_NullInput_ReturnsEmptyString()
        {
            // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            string result = StringHelpers.UrlKey(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("#StartingHash", "startinghash")]
        [InlineData("TrailingHash#", "trailinghash")]
        [InlineData("#BothSides#", "bothsides")]
        public void UrlKey_RemovesLeadingAndTrailingHashes(string input, string expected)
        {
            // Act
            string result = StringHelpers.UrlKey(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("----Multiple----dashes----", "multiple-dashes")]
        [InlineData("No-change-needed", "no-change-needed")]
        public void UrlKey_CollapsesMultipleSpacesAndDashes(string input, string expected)
        {
            // Act
            string result = StringHelpers.UrlKey(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
