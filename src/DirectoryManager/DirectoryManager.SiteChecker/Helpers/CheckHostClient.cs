using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace DirectoryManager.SiteChecker.Helpers
{
    // Second-opinion reachability check via check-host.net's public API.
    // Called ONLY when a direct check fails at the connection level (the
    // signature of our own VPS IP being blocked). check-host runs the request
    // from global nodes whose IPs aren't blocked, so it tells us whether the
    // host is actually up. Never called for normal checks or 404s — that keeps
    // us well inside check-host's rate limits.
    public sealed class CheckHostClient
    {
        private const string ApiBase = "https://check-host.net";
        private const int MaxNodes = 5;

        // check-host limits how fast a single client may submit checks, so we
        // serialize second-opinion calls.
        private static readonly SemaphoreSlim Throttle = new (1, 1);

        private readonly HttpClient http;
        private readonly DiagnosticLogger log;

        public CheckHostClient(DiagnosticLogger? logger = null)
        {
            this.log = logger ?? new DiagnosticLogger(null);

            // Use the same Happy Eyeballs connect as the main checker, so the
            // call to check-host isn't itself tripped up by the box's broken v6.
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                ConnectCallback = async (context, ct) =>
                {
                    var socket = await HappyEyeballs
                        .ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, TimeSpan.FromSeconds(10), ct)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };

            this.http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            this.http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        // True if any check-host node reaches the URL with a 2xx/3xx response.
        public async Task<bool> IsUpAsync(Uri uri)
        {
            await Throttle.WaitAsync().ConfigureAwait(false);
            try
            {
                var startUrl = $"{ApiBase}/check-http?host={Uri.EscapeDataString(uri.ToString())}&max_nodes={MaxNodes}";

                string? requestId;
                using (var startResp = await this.http.GetAsync(startUrl).ConfigureAwait(false))
                {
                    if (!startResp.IsSuccessStatusCode)
                    {
                        this.log.Log($"[check-host] start failed for {uri}: HTTP {(int)startResp.StatusCode}");
                        return false;
                    }

                    var startJson = await startResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var startDoc = JsonDocument.Parse(startJson);
                    requestId = startDoc.RootElement.TryGetProperty("request_id", out var idEl)
                        ? idEl.GetString()
                        : null;
                }

                if (string.IsNullOrEmpty(requestId))
                {
                    return false;
                }

                // Nodes report asynchronously over a few seconds — poll until we
                // get a conclusive answer or run out of patience.
                var resultUrl = $"{ApiBase}/check-result/{requestId}";
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

                    using var resultResp = await this.http.GetAsync(resultUrl).ConfigureAwait(false);
                    if (!resultResp.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var resultJson = await resultResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var (anyUp, allReported) = ParseResults(resultJson);

                    if (anyUp)
                    {
                        this.log.Log($"[check-host] {uri} → reachable from an external node");
                        return true;
                    }

                    if (allReported)
                    {
                        this.log.Log($"[check-host] {uri} → no external node could reach it either");
                        return false;
                    }
                }

                this.log.Log($"[check-host] {uri} → timed out waiting for node results");
                return false;
            }
            catch (Exception ex)
            {
                this.log.Log($"[check-host] error for {uri}: {DiagnosticLogger.SummarizeException(ex)}");
                return false;
            }
            finally
            {
                Throttle.Release();
            }
        }

        // Each node value is an array holding one result array of the form
        // [success, time, message, statusCode, ip]. success==1 with a 2xx/3xx
        // status means that node reached the host. A null node value means that
        // node hasn't reported yet.
        private static (bool anyUp, bool allReported) ParseResults(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (false, false);
            }

            bool anyUp = false;
            bool allReported = true;
            int nodeCount = 0;

            foreach (var node in doc.RootElement.EnumerateObject())
            {
                nodeCount++;
                var val = node.Value;

                if (val.ValueKind == JsonValueKind.Null)
                {
                    allReported = false;
                    continue;
                }

                if (val.ValueKind != JsonValueKind.Array || val.GetArrayLength() == 0)
                {
                    continue;
                }

                var entry = val[0];
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 4)
                {
                    continue;
                }

                var success = entry[0];
                var statusEl = entry[3];

                bool ok = success.ValueKind == JsonValueKind.Number
                    && success.TryGetInt32(out var s)
                    && s == 1;

                if (ok && statusEl.ValueKind == JsonValueKind.String)
                {
                    var status = statusEl.GetString();
                    if (!string.IsNullOrEmpty(status) && (status[0] == '2' || status[0] == '3'))
                    {
                        anyUp = true;
                    }
                }
            }

            if (nodeCount == 0)
            {
                allReported = false;
            }

            return (anyUp, allReported);
        }
    }
}