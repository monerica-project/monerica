using System.Net;

namespace DirectoryManager.ReviewModerator.Fetching
{
    public sealed class FetchResult
    {
        public bool Success { get; init; }

        public int StatusCode { get; init; }

        public string Body { get; init; } = string.Empty;

        public string ContentType { get; init; } = string.Empty;

        public string? Error { get; init; }
    }

    public interface IOrderProofFetcher
    {
        /// <summary>Fetch the body of a proof page/API. Uses Tor automatically for .onion hosts.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task<FetchResult> GetAsync(Uri uri, CancellationToken ct = default);
    }
}
