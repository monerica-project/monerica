using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Tests.Helpers
{
    public class SubmissionDecisionTests
    {
        [Fact]
        public void ResolveReason_ListedReason_UsesItsLabel()
        {
            var text = SubmissionDecision.ResolveReason("duplicate", null, out var error);
            Assert.Null(error);
            Assert.Equal("Duplicate of an existing listing or submission", text);
        }

        [Fact]
        public void ResolveReason_ListedReasonWithDetail_KeepsTheDetail()
        {
            var text = SubmissionDecision.ResolveReason("duplicate", "  same as #812 ", out var error);
            Assert.Null(error);
            Assert.Equal("Duplicate of an existing listing or submission: same as #812", text);
        }

        [Fact]
        public void ResolveReason_Other_NeedsText()
        {
            Assert.Null(SubmissionDecision.ResolveReason("other", "   ", out var error));
            Assert.NotNull(error);

            Assert.Equal("Shop only sells gift cards", SubmissionDecision.ResolveReason("other", "Shop only\nsells gift cards", out error));
            Assert.Null(error);
        }

        [Fact]
        public void ResolveReason_TextWithoutPickingOther_CountsAsOther()
        {
            Assert.Equal("typed only", SubmissionDecision.ResolveReason(null, "typed only", out var error));
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-reason")]
        public void ResolveReason_NoOrUnknownChoice_IsAnError(string? key)
        {
            Assert.Null(SubmissionDecision.ResolveReason(key, null, out var error));
            Assert.NotNull(error);
        }

        [Fact]
        public void ResolveReason_TooLong_IsAnError()
        {
            var text = new string('x', SubmissionDecision.OtherReasonMaxLength + 1);
            Assert.Null(SubmissionDecision.ResolveReason("other", text, out var error));
            Assert.NotNull(error);
        }

        [Fact]
        public void Reasons_HaveUniqueKeys_AndNoneIsOther()
        {
            var keys = SubmissionDecision.Reasons.Select(r => r.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.DoesNotContain(SubmissionDecision.OtherReasonKey, keys);
        }

        [Fact]
        public void StatusFor_SpamIsSpam_EverythingElseRejected()
        {
            Assert.Equal(SubmissionStatus.Spam, SubmissionDecision.StatusFor("spam"));
            Assert.Equal(SubmissionStatus.Rejected, SubmissionDecision.StatusFor("duplicate"));
            Assert.Equal(SubmissionStatus.Rejected, SubmissionDecision.StatusFor("other"));
        }

        [Theory]
        [InlineData("pending", "/submission/pending")]
        [InlineData("AI-REVIEW", "/admin/submission-review")]
        [InlineData("all", "/submission/index")]
        [InlineData("https://evil.example/", "/submission/pending")]
        [InlineData("//evil.example", "/submission/pending")]
        [InlineData(null, "/submission/pending")]
        public void ReturnPath_OnlyKnownLists(string? from, string expected)
        {
            Assert.Equal(expected, SubmissionDecision.ReturnPath(from, SubmissionDecision.FromPending));
        }

        [Fact]
        public void MessageKey_AiReviewPageHasItsOwnKey()
        {
            Assert.Equal("AiReviewMessage", SubmissionDecision.MessageKey("ai-review"));
            Assert.Equal("SuccessMessage", SubmissionDecision.MessageKey("pending"));
        }

        [Theory]
        [InlineData("AutoReject: Spam: empty description", "spam")]
        [InlineData("AutoReject: Already listed as \"X\" (#4)", "duplicate")]
        [InlineData("AutoReject: Same URL already submitted (submission #9)", "duplicate")]
        [InlineData("AutoReject: URL unreachable after one retry (timeout)", "not-working")]
        [InlineData(null, null)]
        public void SuggestReasonKey_FromPrefilter(string? prefilter, string? expected)
        {
            Assert.Equal(expected, SubmissionDecision.SuggestReasonKey(prefilter));
        }

        [Fact]
        public void QuickApprove_WithSensitiveChange_NeedsConfirmation()
        {
            var entry = new DirectoryEntry { Name = "x", DirectoryEntryKey = "x", Link = "https://a.example", PgpKey = "KEY-A", DirectoryStatus = DirectoryStatus.Verified };
            var sub = new Submission { Name = "x", Link = "https://a.example", PgpKey = "KEY-B" };
            var sensitive = SubmissionProtectedFields.SensitiveChanges(entry, sub);

            Assert.NotEmpty(sensitive);
            Assert.NotNull(SubmissionDecision.QuickApproveBlockReason(sensitive, confirmed: false));
            Assert.Null(SubmissionDecision.QuickApproveBlockReason(sensitive, confirmed: true));
            Assert.Null(SubmissionDecision.QuickApproveBlockReason(new List<string>(), confirmed: false));
        }
    }
}
