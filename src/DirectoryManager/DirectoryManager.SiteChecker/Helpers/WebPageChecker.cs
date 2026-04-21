using System.Diagnostics;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class WebPageChecker
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private readonly HttpClient http;
        private readonly DiagnosticLogger log;

        public WebPageChecker(string userAgent, TimeSpan? timeout = null, DiagnosticLogger? logger = null)
        {
            this.http = new HttpClient
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            this.log = logger ?? new DiagnosticLogger(null);
        }

        public async Task<bool> IsOnlineAsync(Uri uri)
        {
            var attemptSummaries = new List<string>();

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                var (result, summary) = await this.TryOnceAsync(uri, attempt);
                attemptSummaries.Add(summary);

                if (result.HasValue)
                {
                    if (!result.Value)
                    {
                        // Definitive offline (e.g. 404/410/521). Still log it so you can verify.
                        this.log.LogOfflineFailure(uri.ToString(), "clearnet", attemptSummaries);
                    }

                    return result.Value;
                }

                if (attempt < MaxRetries)
                {
                    this.log.Log($"[clearnet] Retrying {uri} in {RetryDelay.TotalSeconds}s (attempt {attempt}/{MaxRetries})...");
                    await Task.Delay(RetryDelay);
                }
            }

            // All HTTP attempts inconclusive. Before giving up, try a raw TCP probe.
            // If the server accepts a TCP connection, it's up — our TLS/HTTP stack just can't talk to it.
            bool tcpOk = await this.ProbeTcpAsync(uri);
            if (tcpOk)
            {
                this.log.Log($"[clearnet] {uri} → ONLINE (via TCP probe; HTTP inconclusive but port is open)");
                return true;
            }

            this.log.LogOfflineFailure(uri.ToString(), "clearnet", attemptSummaries);
            return false;
        }

        private async Task<bool> ProbeTcpAsync(Uri uri)
        {
            try
            {
                var port = uri.IsDefaultPort
                    ? (uri.Scheme == "https" ? 443 : 80)
                    : uri.Port;

                using var tcp = new System.Net.Sockets.TcpClient();
                var connectTask = tcp.ConnectAsync(uri.Host, port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

                var winner = await Task.WhenAny(connectTask, timeoutTask);
                if (winner == connectTask && tcp.Connected)
                {
                    this.log.Log($"[clearnet TCP probe] {uri.Host}:{port} → open (server is up, TLS/HTTP layer failing)");
                    return true;
                }

                this.log.Log($"[clearnet TCP probe] {uri.Host}:{port} → no connection");
                return false;
            }
            catch (Exception ex)
            {
                this.log.Log($"[clearnet TCP probe] {uri.Host} → failed: {ex.Message}");
                return false;
            }
        }

        private async Task<(bool? result, string summary)> TryOnceAsync(Uri uri, int attempt)
        {
            // 1) HEAD
            var headSw = Stopwatch.StartNew();
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, uri);
                using var headResp = await this.http
                    .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                headSw.Stop();
                var statusCode = (int)headResp.StatusCode;
                var server = headResp.Headers.TryGetValues("Server", out var sv) ? string.Join(",", sv) : "";
                var cfRay = headResp.Headers.TryGetValues("CF-Ray", out var cr) ? string.Join(",", cr) : "";

                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} → {statusCode} ({headSw.ElapsedMilliseconds}ms) Server={server} CF-Ray={cfRay}");

                if (statusCode is >= 200 and < 400)
                {
                    return (true, $"attempt {attempt}: HEAD {statusCode} in {headSw.ElapsedMilliseconds}ms");
                }

                if (statusCode == 521)
                {
                    return (false, $"attempt {attempt}: HEAD 521 in {headSw.ElapsedMilliseconds}ms");
                }

                // fall through
            }
            catch (TaskCanceledException)
            {
                headSw.Stop();
                var summary = $"attempt {attempt}: HEAD timeout after {headSw.ElapsedMilliseconds}ms";
                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                headSw.Stop();
                var headFailSummary = $"HEAD failed ({headSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[clearnet HEAD attempt {attempt}] {uri} — {headFailSummary}");
                // fall through to GET
            }

            // 2) GET
            var getSw = Stopwatch.StartNew();
            try
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResp = await this.http
                    .SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                getSw.Stop();
                var statusCode = (int)getResp.StatusCode;
                var server = getResp.Headers.TryGetValues("Server", out var sv) ? string.Join(",", sv) : "";
                var cfRay = getResp.Headers.TryGetValues("CF-Ray", out var cr) ? string.Join(",", cr) : "";

                this.log.Log($"[clearnet GET attempt {attempt}] {uri} → {statusCode} ({getSw.ElapsedMilliseconds}ms) Server={server} CF-Ray={cfRay}");

                if (statusCode == 404 || statusCode == 410)
                {
                    return (false, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms Server={server}");
                }

                return (true, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms");
            }
            catch (TaskCanceledException)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET timeout after {getSw.ElapsedMilliseconds}ms";
                this.log.Log($"[clearnet GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET failed ({getSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[clearnet GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
        }
    }
}