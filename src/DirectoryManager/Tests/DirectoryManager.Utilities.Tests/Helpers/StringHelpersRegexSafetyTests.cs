using System.Diagnostics;
using DirectoryManager.Utilities.Helpers;
using Xunit;

namespace DirectoryManager.Utilities.Tests.Helpers
{
    /// <summary>
    /// Audit 2026-11-11: one auto-approved review could make a listing page return 500 for everyone
    /// (link/email regex timed out on a run of '%' characters). Rendering must stay fast and never throw.
    /// </summary>
    public class StringHelpersRegexSafetyTests
    {
        private static readonly string Payload = "Bad service " + new string('%', 7000) + "@" + string.Concat(Enumerable.Repeat("1.", 1490));

        [Fact] // SAD: the audit's payload renders (no exception) and quickly
        public void ReviewBody_PathologicalInput_DoesNotThrow()
        {
            var sw = Stopwatch.StartNew();
            var html = StringHelpers.RenderBodyWithLinksHtml(Payload, "multi-line-text", ugcContent: true);
            sw.Stop();

            Assert.StartsWith("Bad service", html, StringComparison.Ordinal);
            Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
        }

        [Fact] // SAD: same for contact fields
        public void ContactField_PathologicalInput_DoesNotThrow()
        {
            var html = StringHelpers.RenderContactFieldHtml(Payload, "contact");
            Assert.Contains("Bad service", html, StringComparison.Ordinal);
        }

        [Fact] // HAPPY: normal links and masked emails still work
        public void ReviewBody_NormalText_StillLinkifiesAndMasks()
        {
            var html = StringHelpers.RenderBodyWithLinksHtml("See https://example.com or mail jane.doe%test@example.com", "multi-line-text", ugcContent: true);

            Assert.Contains("href=\"https://example.com\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("jane.doe%test@example.com", html, StringComparison.Ordinal);
        }
    }
}
