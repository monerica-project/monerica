using DirectoryManager.DisplayFormatting.Helpers;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// "Loaded" guarantee: even if hostile markup somehow reaches storage, the
    /// render path must never emit something a browser will execute.
    ///
    ///   ContentSanitizer.Sanitize  -> OUTPUT allowlist (keeps safe formatting).
    ///   ContentSanitizer.StripHtml -> INPUT strip (returns plain text).
    ///
    /// Assertions are invariant-based (no executable token survives; allowed
    /// formatting is preserved) so they don't go brittle across sanitizer versions.
    /// </summary>
    public class ContentSanitizerTests
    {
        // These tokens must NEVER appear in sanitized OUTPUT, regardless of input.
        private static readonly string[] ForbiddenInOutput =
        {
            "<script", "</script", "javascript:", "vbscript:",
            "onerror", "onload", "onclick", "onmouseover", "onfocus", "ontoggle",
            "<iframe", "<object", "<embed", "<svg", "<img", "<meta", "<link", "<base", "<form",
        };

        public static IEnumerable<object[]> HostilePayloads()
        {
            yield return new object[] { "<script>alert(1)</script>" };
            yield return new object[] { "<img src=x onerror=alert(1)>" };
            yield return new object[] { "<svg onload=alert(1)>" };
            yield return new object[] { "<iframe src=//evil.example></iframe>" };
            yield return new object[] { "<a href=\"javascript:alert(1)\">click</a>" };
            yield return new object[] { "<a href=\"vbscript:msgbox(1)\">click</a>" };
            yield return new object[] { "<div onmouseover=\"alert(1)\">hi</div>" };
            yield return new object[] { "<input autofocus onfocus=alert(1)>" };
            yield return new object[] { "<details open ontoggle=alert(1)>x</details>" };
            yield return new object[] { "<meta http-equiv=refresh content=0;url=//evil.example>" };
            yield return new object[] { "<form action=//evil.example><input></form>" };
            yield return new object[] { "Hello <script>steal()</script> world" };
        }

        [Theory]
        [MemberData(nameof(HostilePayloads))]
        public void Sanitize_StripsAllExecutableMarkup(string payload)
        {
            var output = ContentSanitizer.Sanitize(payload).ToLowerInvariant();

            foreach (var token in ForbiddenInOutput)
            {
                Assert.DoesNotContain(token, output, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData("<b>bold</b>", "<b>")]
        [InlineData("<strong>x</strong>", "<strong>")]
        [InlineData("<em>x</em>", "<em>")]
        [InlineData("<ul><li>one</li></ul>", "<li>")]
        [InlineData("<p>para</p>", "<p>")]
        [InlineData("<code>x</code>", "<code>")]
        public void Sanitize_PreservesAllowlistedFormatting(string input, string expectedTagFragment)
        {
            var output = ContentSanitizer.Sanitize(input);

            Assert.Contains(expectedTagFragment, output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sanitize_KeepsHttpLink_ButDropsJavascriptScheme()
        {
            var safe = ContentSanitizer.Sanitize("<a href=\"https://example.com\">x</a>");
            Assert.Contains("https://example.com", safe, StringComparison.OrdinalIgnoreCase);

            var hostile = ContentSanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>");
            Assert.DoesNotContain("javascript:", hostile, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Sanitize_HardensBlankTargetLinks_AgainstTabnabbing()
        {
            var output = ContentSanitizer.Sanitize(
                "<a href=\"https://example.com\" target=\"_blank\">x</a>");

            Assert.Contains("noopener", output, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sanitize_EmptyOrNull_ReturnsEmpty(string? input)
        {
            Assert.Equal(string.Empty, ContentSanitizer.Sanitize(input));
        }

        // ---- StripHtml: public-input path. No markup may survive at all. ----

        [Theory]
        [MemberData(nameof(HostilePayloads))]
        public void StripHtml_RemovesEveryTag(string payload)
        {
            var output = ContentSanitizer.StripHtml(payload);

            Assert.DoesNotContain("<", output);
            Assert.DoesNotContain(">", output);
        }

        [Fact]
        public void StripHtml_KeepsVisibleText()
        {
            var output = ContentSanitizer.StripHtml("<b>Accepts</b> <i>XMR</i>");

            Assert.Contains("Accepts", output);
            Assert.Contains("XMR", output);
            Assert.DoesNotContain("<", output);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void StripHtml_EmptyOrNull_ReturnsEmpty(string? input)
        {
            Assert.Equal(string.Empty, ContentSanitizer.StripHtml(input));
        }
    }
}
