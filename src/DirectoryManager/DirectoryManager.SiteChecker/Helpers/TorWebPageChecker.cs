using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class TorWebPageChecker
    {
        private const int MaxRetries = 2;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private readonly HttpClient http;
        private readonly string torHost;
        private readonly int torPort;
        private readonly DiagnosticLogger log;

        public TorWebPageChecker(
            string userAgent,
            string torHost = "127.0.0.1",
            int torPort = 9050,
            TimeSpan? timeout = null,
            DiagnosticLogger? logger = null)
        {
            this.torHost = torHost;
            this.torPort = torPort;
            this.log = logger ?? new DiagnosticLogger(null);

            var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"socks5://{torHost}:{torPort}"),
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(90),
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (m, c, ch, e) => true
                }
            };

            this.http = new HttpClient(handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(60)
            };
            this.http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        public static bool IsTorAvailable(string torHost = "127.0.0.1", int torPort = 9050)
        {
            try
            {
                using var tcp = new TcpClient();
                tcp.Connect(torHost, torPort);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsTorAvailable() => IsTorAvailable(this.torHost, this.torPort);

        public static async Task<bool> TryStartTorAsync(
            string torExecutablePath,
            string torHost = "127.0.0.1",
            int torPort = 9050,
            int startupWaitMs = 30000)
        {
            if (IsTorAvailable(torHost, torPort))
            {
                Console.WriteLine("[TOR] Already running.");
                return true;
            }

            if (!File.Exists(torExecutablePath))
            {
                Console.WriteLine($"[TOR] tor.exe not found at {torExecutablePath} — .onion links will be skipped (not marked offline).");
                return false;
            }

            try
            {
                Console.WriteLine($"[TOR] Starting {torExecutablePath}...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = torExecutablePath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();

                Console.WriteLine($"[TOR] Waiting up to {startupWaitMs / 1000}s for Tor to bootstrap...");
                var deadline = DateTime.UtcNow.AddMilliseconds(startupWaitMs);
                while (DateTime.UtcNow < deadline)
                {
                    if (IsTorAvailable(torHost, torPort))
                    {
                        Console.WriteLine("[TOR] Started successfully.");
                        return true;
                    }

                    await Task.Delay(2000);
                }

                Console.WriteLine("[TOR] Failed to start within timeout.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOR] Could not start tor.exe: {ex.Message}");
                return false;
            }
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
                        this.log.LogOfflineFailure(uri.ToString(), "tor", attemptSummaries);
                    }

                    return result.Value;
                }

                if (attempt < MaxRetries)
                {
                    this.log.Log($"[TOR] Retrying {uri} in {RetryDelay.TotalSeconds}s (attempt {attempt}/{MaxRetries})...");
                    await Task.Delay(RetryDelay);
                }
            }

            this.log.LogOfflineFailure(uri.ToString(), "tor", attemptSummaries);
            return false;
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
                this.log.Log($"[TOR HEAD attempt {attempt}] {uri} → {statusCode} ({headSw.ElapsedMilliseconds}ms)");

                if (statusCode is >= 200 and < 400)
                {
                    return (true, $"attempt {attempt}: HEAD {statusCode} in {headSw.ElapsedMilliseconds}ms");
                }

                if (statusCode == 521)
                {
                    return (false, $"attempt {attempt}: HEAD 521 in {headSw.ElapsedMilliseconds}ms");
                }
            }
            catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                headSw.Stop();
                // fall through to GET
            }
            catch (TaskCanceledException)
            {
                headSw.Stop();
                var summary = $"attempt {attempt}: HEAD timeout after {headSw.ElapsedMilliseconds}ms";
                this.log.Log($"[TOR HEAD attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                headSw.Stop();
                var summary = $"attempt {attempt}: HEAD failed ({headSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[TOR HEAD attempt {attempt}] {uri} — {summary}");
                return (null, summary);
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
                this.log.Log($"[TOR GET attempt {attempt}] {uri} → {statusCode} ({getSw.ElapsedMilliseconds}ms)");

                if (statusCode == 404 || statusCode == 410)
                {
                    return (false, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms");
                }

                return (true, $"attempt {attempt}: GET {statusCode} in {getSw.ElapsedMilliseconds}ms");
            }
            catch (TaskCanceledException)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET timeout after {getSw.ElapsedMilliseconds}ms";
                this.log.Log($"[TOR GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
            catch (Exception ex)
            {
                getSw.Stop();
                var summary = $"attempt {attempt}: GET failed ({getSw.ElapsedMilliseconds}ms): {DiagnosticLogger.SummarizeException(ex)}";
                this.log.Log($"[TOR GET attempt {attempt}] {uri} — {summary}");
                return (null, summary);
            }
        }
    }
}