using System.Net;
using System.Net.NetworkInformation;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class WebPageChecker
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private readonly HttpClient http;

        public WebPageChecker(string userAgent, TimeSpan? timeout = null)
        {
            this.http = new HttpClient
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        public async Task<bool> IsOnlineAsync(Uri uri)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                bool? result = await this.TryOnceAsync(uri, attempt);

                if (result.HasValue)
                {
                    return result.Value;
                }

                if (attempt < MaxRetries)
                {
                    Console.WriteLine($"[clearnet] Retrying {uri} in {RetryDelay.TotalSeconds}s (attempt {attempt}/{MaxRetries})...");
                    await Task.Delay(RetryDelay);
                }
            }

            Console.WriteLine($"[clearnet] {uri} — all {MaxRetries} attempts failed, treating as offline.");
            return false;
        }

        private async Task<bool?> TryOnceAsync(Uri uri, int attempt)
        {
            // 1) Try HEAD
            // - Success (2xx/3xx)  → online
            // - 521                → offline
            // - Timeout            → null (retry the whole attempt)
            // - 405 or any error   → fall through to GET
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, uri);
                using var headResp = await this.http
                    .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                var statusCode = (int)headResp.StatusCode;
                Console.WriteLine($"[clearnet HEAD attempt {attempt}] {uri} → {statusCode}");

                if (statusCode is >= 200 and < 400)
                {
                    return true;
                }

                if (statusCode == 521)
                {
                    return false;
                }

                // 403/404/429/503 etc. — fall through to GET
            }
            catch (TaskCanceledException)
            {
                // Timeout is worth retrying — bail out of this attempt entirely
                Console.WriteLine($"[clearnet HEAD attempt {attempt}] {uri} — timed out.");
                return null;
            }
            catch (Exception ex)
            {
                // Connection error, DNS failure, etc. — HEAD won't work but GET might,
                // so fall through rather than wasting retries on HEAD
                Console.WriteLine($"[clearnet HEAD attempt {attempt}] {uri}: {ex.Message} — falling through to GET");
            }

            // 2) Try GET
            // - 404/410   → offline
            // - Timeout   → null (retry)
            // - Any other → online
            try
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResp = await this.http
                    .SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                var statusCode = (int)getResp.StatusCode;
                Console.WriteLine($"[clearnet GET attempt {attempt}] {uri} → {statusCode}");

                if (statusCode == 404 || statusCode == 410)
                {
                    return false;
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[clearnet GET attempt {attempt}] {uri} — timed out.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[clearnet GET attempt {attempt}] {uri}: {ex.Message}");
                return null;
            }
        }
    }
}