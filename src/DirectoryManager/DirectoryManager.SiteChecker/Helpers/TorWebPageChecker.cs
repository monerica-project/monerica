using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class TorWebPageChecker
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

        private readonly HttpClient http;
        private readonly string torHost;
        private readonly int torPort;

        public TorWebPageChecker(string userAgent, string torHost = "127.0.0.1", int torPort = 9050, TimeSpan? timeout = null)
        {
            this.torHost = torHost;
            this.torPort = torPort;

            var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"socks5://{torHost}:{torPort}"),
                UseProxy = true,
                ConnectTimeout = TimeSpan.FromSeconds(150),
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                    {
                        if (sslPolicyErrors != SslPolicyErrors.None)
                        {
                            Console.WriteLine($"[TOR SSL warning] {sslPolicyErrors}");
                        }

                        return true;
                    }
                }
            };

            this.http = new HttpClient(handler)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(180)
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
            // Already running — nothing to do
            if (IsTorAvailable(torHost, torPort))
            {
                Console.WriteLine("[TOR] Already running.");
                return true;
            }

            // exe not found — skip silently, do not treat onion links as offline
            if (!File.Exists(torExecutablePath))
            {
                Console.WriteLine($"[TOR] tor.exe not found at {torExecutablePath} — .onion links will be skipped (not marked offline).");
                return false;
            }

            try
            {
                Console.WriteLine($"[TOR] Starting {torExecutablePath}...");

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = torExecutablePath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();

                // Poll for Tor to become available rather than blindly waiting
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

                Console.WriteLine("[TOR] Failed to start within timeout — .onion links will be skipped (not marked offline).");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOR] Could not start tor.exe: {ex.Message} — .onion links will be skipped (not marked offline).");
                return false;
            }
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

                // null = inconclusive (timeout/connection error) — retry after a pause
                if (attempt < MaxRetries)
                {
                    Console.WriteLine($"[TOR] Retrying {uri} in {RetryDelay.TotalSeconds}s (attempt {attempt}/{MaxRetries})...");
                    await Task.Delay(RetryDelay);
                }
            }

            // All attempts exhausted with no conclusive response
            Console.WriteLine($"[TOR] {uri} — all {MaxRetries} attempts failed, treating as offline.");
            return false;
        }

        /// <summary>
        /// Returns true/false for a definitive result, or null to signal a retry should be attempted.
        /// </summary>
        private async Task<bool?> TryOnceAsync(Uri uri, int attempt)
        {
            // 1) Try HEAD
            try
            {
                using var headReq = new HttpRequestMessage(HttpMethod.Head, uri);
                using var headResp = await this.http
                    .SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                var statusCode = (int)headResp.StatusCode;
                Console.WriteLine($"[TOR HEAD attempt {attempt}] {uri} → {statusCode}");

                if (statusCode is >= 200 and < 400)
                {
                    return true;
                }

                if (statusCode == 521)
                {
                    return false;
                }

                // 403/429/503 etc. means server IS responding — fall to GET to confirm
            }
            catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                // HEAD not allowed → fall to GET
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[TOR HEAD attempt {attempt}] {uri} — timed out.");
                return null; // signal retry
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOR HEAD attempt {attempt} failed] {uri}: {ex.Message}");
                return null; // signal retry
            }

            // 2) Try GET
            try
            {
                using var getReq = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResp = await this.http
                    .SendAsync(getReq, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                var statusCode = (int)getResp.StatusCode;
                Console.WriteLine($"[TOR GET attempt {attempt}] {uri} → {statusCode}");

                if (statusCode == 404 || statusCode == 410)
                {
                    return false;
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"[TOR GET attempt {attempt}] {uri} — timed out.");
                return null; // signal retry
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TOR GET attempt {attempt} failed] {uri}: {ex.Message}");
                return null; // signal retry
            }
        }
    }
}
