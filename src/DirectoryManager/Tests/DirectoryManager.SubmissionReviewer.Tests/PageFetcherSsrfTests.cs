using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using Xunit;

namespace DirectoryManager.SubmissionReviewer.Tests
{
    /// <summary>Audit 2026-11-11: submitted links must never make the job contact this server's internal addresses.</summary>
    public class PageFetcherSsrfTests
    {
        [Theory] // SAD: loopback, LAN, link-local cloud metadata are refused before any connection is made
        [InlineData("http://127.0.0.1:5432/")]
        [InlineData("http://localhost:5055/")]
        [InlineData("http://10.0.0.1/")]
        [InlineData("http://169.254.169.254/latest/meta-data/")]
        [InlineData("http://[::1]:8081/")]
        public async Task Fetch_RefusesInternalAddresses(string url)
        {
            using var fetcher = new PageFetcher(new ReviewerOptions());

            var result = await fetcher.FetchAsync(url, CancellationToken.None);

            Assert.Null(result.Html);
            Assert.Contains("private or internal", result.Error ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
