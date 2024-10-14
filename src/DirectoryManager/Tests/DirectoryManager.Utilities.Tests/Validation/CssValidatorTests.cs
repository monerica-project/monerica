using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests.Validation
{
    public class CssValidatorTests
    {
        [Fact]
        public void ValidateCss_ValidCss_ShouldReturnTrue()
        {
            // Arrange
            var validCss = @"
                body {
                    background-color: #000000;
                    color: white;
                    margin: 0;
                    padding: 0;
                }

                h1 {
                    font-size: 24px;
                    font-weight: bold;
                }
            ";

            // Act
            var result = CssValidator.ValidateCss(validCss);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_InvalidCss_ShouldReturnFalse()
        {
            // Arrange
            var invalidCss = @"
                body {
                    background-color: #000000;
                    color: white;
                    margin 0; /* Missing colon */
                    padding: 0;
                }

                h1 {
                    font-size: 24px
                    font-weight: bold;
                }
            ";

            // Act
            var result = CssValidator.ValidateCss(invalidCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_EmptyCss_ShouldReturnFalse()
        {
            // Arrange
            var emptyCss = "";

            // Act
            var result = CssValidator.ValidateCss(emptyCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_ValidCssWithExtraSpaces_ShouldReturnTrue()
        {
            // Arrange
            var validCssWithSpaces = @"
                .container   {
                    padding   : 10px;
                    margin : 0  ;
                }
            ";

            // Act
            var result = CssValidator.ValidateCss(validCssWithSpaces);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_MalformedCss_ShouldReturnFalse()
        {
            // Arrange
            var malformedCss = @"
                .button {
                    background-color: #FFF;
                    font-size: 14px;
                /* Missing closing brace */
            ";

            // Act
            var result = CssValidator.ValidateCss(malformedCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_CssWithoutSemicolon_ShouldReturnFalse()
        {
            // Arrange
            var cssWithoutSemicolon = @"
                body {
                    background-color: #000000
                    color: white;
                }
            ";

            // Act
            var result = CssValidator.ValidateCss(cssWithoutSemicolon);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_ValidSingleLineCss_ShouldReturnTrue()
        {
            // Arrange
            var singleLineCss = "body { margin: 0; padding: 0; }";

            // Act
            var result = CssValidator.ValidateCss(singleLineCss);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_CssWithInvalidSelector_ShouldReturnFalse()
        {
            // Arrange
            var cssWithInvalidSelector = @"
                .123invalid-selector {
                    color: red;
                }
            ";

            // Act
            var result = CssValidator.ValidateCss(cssWithInvalidSelector);

            // Assert
            Assert.False(result);
        }
    }
}
