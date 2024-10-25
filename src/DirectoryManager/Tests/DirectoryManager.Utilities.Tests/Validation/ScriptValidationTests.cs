using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests.Validation
{

    public class ScriptValidationTests
    {
        // Helper class to test ScriptValidation with different object structures
        private class TestObject
        {
            public string Content { get; set; } = string.Empty;
            public string OtherProperty { get; set; } = string.Empty;
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnTrue_WhenScriptTagExists()
        {
            // Arrange
            var obj = new TestObject { Content = "<script>alert('test');</script>" };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnTrue_WhenHtmlEncodedScriptTagExists()
        {
            // Arrange
            var obj = new TestObject { Content = "&lt;script&gt;alert('test');&lt;/script&gt;" };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnFalse_WhenNoScriptTagExists()
        {
            // Arrange
            var obj = new TestObject { Content = "<div>Hello World</div>" };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnFalse_WhenPropertyIsEmpty()
        {
            // Arrange
            var obj = new TestObject { Content = "" };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnFalse_WhenPropertyIsNull()
        {
            // Arrange
            var obj = new TestObject { Content = null };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ContainsScriptTag_ShouldReturnFalse_WhenObjectHasNoStringProperties()
        {
            // Arrange
            var obj = new { Number = 123, Date = System.DateTime.Now };

            // Act
            var result = ScriptValidation.ContainsScriptTag(obj);

            // Assert
            Assert.False(result);
        }
    }
}