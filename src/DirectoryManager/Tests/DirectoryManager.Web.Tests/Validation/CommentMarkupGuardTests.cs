using System.ComponentModel.DataAnnotations;
using DirectoryManager.Utilities.Validation;
using DirectoryManager.Web.Models.ProblemReports;
using DirectoryManager.Web.Models.VerificationRequests;

namespace DirectoryManager.Web.Tests.Validation
{
    /// <summary>
    /// The verification-request and problem-report comment boxes are plain text: ordinary links
    /// and prose must pass, HTML and JavaScript must not.
    /// </summary>
    public class CommentMarkupGuardTests
    {
        [Theory]
        [InlineData("The site has been down for a week, see https://example.com/status?online=1&ref=a#top")]
        [InlineData("Proof: <https://example.com/thread/123>")]
        [InlineData("http://exampleonionaddressxyz.onion/page and https://bitcointalk.org/index.php?topic=1.0")]
        [InlineData("My data: name and email were leaked. Price was 5 < 10 and 10 > 5.")]
        [InlineData("Their onion = http://abc.onion is offline; online since 2021 = false claim")]
        [InlineData("Operator replied 'no refund' -- I paid 0.5 XMR (tx: abc123).")]
        public void PlainTextAndLinks_AreAllowed(string comment)
        {
            Assert.False(InputHtmlGuard.ContainsMarkupAllowingUrls(comment));
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("hello <b>bold</b>")]
        [InlineData("<a href=\"https://example.com\">click</a>")]
        [InlineData("<img src=x onerror=alert(1)")]
        [InlineData("<b onmouseover=alert(1) ")]
        [InlineData("see javascript:alert(1)")]
        [InlineData("java\tscript:alert(1)")]
        [InlineData("data:text/html,<h1>x</h1>")]
        [InlineData("data:text/html;base64,PGgxPng8L2gxPg==")]
        [InlineData("&lt;script&gt;alert(1)&lt;/script&gt;")]
        [InlineData("&amp;lt;img src=x&amp;gt;")]
        [InlineData("x\" onfocus=alert(1) autofocus=\"")]
        [InlineData("<https://example.com/><script>alert(1)</script>")]
        [InlineData("<style>body{display:none}</style>")]
        public void HtmlAndJavaScript_AreBlocked(string comment)
        {
            Assert.True(InputHtmlGuard.ContainsMarkupAllowingUrls(comment));
        }

        [Fact]
        public void ProblemReportModel_RejectsHtml_AcceptsLink()
        {
            Assert.NotEmpty(Validate(new CreateProblemReportInputModel { Comment = "Broken <img src=x onerror=alert(1)> page" }));
            Assert.Empty(Validate(new CreateProblemReportInputModel { Comment = "Checkout is broken, see https://example.com/checkout?step=2" }));
        }

        [Fact]
        public void VerificationRequestModel_RejectsHtml_AcceptsLink()
        {
            Assert.NotEmpty(Validate(new CreateVerificationRequestInputModel { Comment = "Please verify <script>alert(1)</script>" }));
            Assert.Empty(Validate(new CreateVerificationRequestInputModel { Comment = "I am the owner, proof at https://example.com/proof.txt" }));
        }

        private static List<ValidationResult> Validate(object model)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }
    }
}
