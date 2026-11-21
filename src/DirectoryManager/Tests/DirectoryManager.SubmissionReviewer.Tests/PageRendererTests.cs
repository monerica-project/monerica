using DirectoryManager.Data.Models;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    public class PageRendererTests
    {
        private const string Shell = "<html><head><title>React App</title></head><body><div id=\"root\"></div><script src=\"/main.js\"></script></body></html>";

        private static FetchResult Page(string? html, int? status = 200) =>
            new ("https://example.com/", true, status, html, null, new Uri("https://example.com/"));

        [Fact] // The IceSwap case: an empty JavaScript shell needs the browser
        public void NeedsRender_ForAnEmptyJavaScriptShell() => Assert.True(PageRenderer.NeedsRender(Page(Shell)));

        [Fact]
        public void NeedsRender_ForABlockedPage() => Assert.True(PageRenderer.NeedsRender(Page(null, 403)));

        [Fact] // A normal page with real text is left alone
        public void NeedsRender_NotForAReadablePage()
        {
            var html = "<html><body><p>" + string.Join(" ", Enumerable.Repeat("We sell hosting and accept Monero at checkout.", 10)) + "</p></body></html>";
            Assert.False(PageRenderer.NeedsRender(Page(html)));
        }

        [Fact] // A dead site is not sent to the browser
        public void NeedsRender_NotForAnUnreachableSite() =>
            Assert.False(PageRenderer.NeedsRender(new FetchResult("https://example.com/", false, null, null, "Timed out", null)));

        [Fact]
        public void Merge_AddsBrowserTextAndKeepsTheMarkup()
        {
            var merged = PageRenderer.Merge(Page(Shell), new RenderedText("IceSwap", "Swap BTC to XMR\nPay with Monero <now>"));
            Assert.Contains("/main.js", merged.Html);
            var body = HtmlText.Extract(merged.Html).Body;
            Assert.Contains("Swap BTC to XMR", body);
            Assert.Contains("Pay with Monero", body);
            Assert.DoesNotContain("<now>", merged.Html); // page text is encoded, never markup
        }

        [Fact]
        public void Merge_TurnsABlockedPageIntoAReadableOne()
        {
            var merged = PageRenderer.Merge(Page(null, 403), new RenderedText("Shop", "We accept XMR"));
            Assert.False(merged.Blocked);
            Assert.Contains("We accept XMR", HtmlText.Extract(merged.Html).Body);
        }

        [Theory]
        [InlineData("{\"ok\":false,\"text\":\"\",\"error\":\"timeout\"}")]
        [InlineData("{\"ok\":true,\"text\":\"  \"}")]
        [InlineData("not json at all")]
        [InlineData(null)]
        public void Parse_ReturnsNullWhenThereIsNothingToRead(string? json) => Assert.Null(PageRenderer.Parse(json));

        [Fact]
        public void Parse_ReadsTitleAndText()
        {
            var r = PageRenderer.Parse("warning line\n{\"ok\":true,\"title\":\"T\",\"text\":\"Pay with Monero\"}");
            Assert.Equal("T", r!.Title);
            Assert.Equal("Pay with Monero", r.Text);
        }

        [Fact] // The VividVPS case: processor left empty, the site shows NOWPayments
        public void Mapper_FillsAnEmptyProcessorFromTheKnownList()
        {
            var s = new Submission { SubmissionId = 1, Name = "VividVPS", Link = "https://vividvps.com" };
            var v = new AiVerdict { Verdict = "approve_candidate", Proposal = new AiProposal { Processor = "nowpayments" } };
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(), new[] { "BTCPay Server", "NOWPayments" });
            Assert.Equal("NOWPayments", s.Processor);
        }

        [Fact]
        public void Mapper_NeverReplacesTheSubmittedProcessor()
        {
            var s = new Submission { SubmissionId = 1, Name = "X", Link = "https://x.example", Processor = "BTCPay Server" };
            var v = new AiVerdict { Verdict = "approve_candidate", Proposal = new AiProposal { Processor = "NOWPayments" } };
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(), new[] { "BTCPay Server", "NOWPayments" });
            Assert.Equal("BTCPay Server", s.Processor);
        }

        [Fact]
        public void Mapper_IgnoresAProcessorTheDirectoryDoesNotKnow()
        {
            var s = new Submission { SubmissionId = 1, Name = "X", Link = "https://x.example" };
            var v = new AiVerdict { Verdict = "approve_candidate", Proposal = new AiProposal { Processor = "Some Made Up Pay" } };
            ProposalMapper.Apply(s, v, new Dictionary<string, int>(), new[] { "BTCPay Server", "NOWPayments" });
            Assert.Null(s.Processor);
        }

        [Fact]
        public void Prompt_TellsTheReviewerWhereTheProcessorGoes()
        {
            var p = ClaudeReviewService.SystemPrompt(new ReviewContext(new[] { "Shopping > Gifts" }, Array.Empty<string>(), Array.Empty<string>()) { ProcessorNames = new[] { "NOWPayments", "BTCPay Server" } });
            Assert.Contains("NOWPayments, BTCPay Server", p);
            Assert.Contains("never name or describe the payment processor", p);
            Assert.Contains("\"processor\"", ClaudeReviewService.Schema().ToJsonString());
        }
    }
}
