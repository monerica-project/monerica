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

        // Tri-state second opinion:
        //   Online       — at least one check-host node reached the URL with a 2xx/3xx.
        //   Offline      — every node reported back and none could reach it (confirmed down).
        //   Inconclusive — check-host itself failed (HTTP error, rate limit, bad JSON, timeout
        //                  waiting for nodes). That says nothing about the site, so callers must
        //                  NOT treat it as "down".
        public async Task<CheckOutcome> CheckAsync(Uri uri)
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
                        return CheckOutcome.Inconclusive;
                    }

                    var startJson = await startResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var startDoc = JsonDocument.Parse(startJson);
                    requestId = startDoc.RootElement.TryGetProperty("request_id", out var idEl)
                        ? idEl.GetString()
                        : null;
                }

                if (string.IsNullOrEmpty(requestId))
                {
                    this.log.Log($"[check-host] no request id for {uri}");
                    return CheckOutcome.Inconclusive;
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
                    var outcome = InterpretResults(resultJson);

                    if (outcome == CheckOutcome.Online)
                    {
                        this.log.Log($"[check-host] {uri} → reachable from an external node");
                        return outcome;
                    }

                    if (outcome == CheckOutcome.Offline)
                    {
                        this.log.Log($"[check-host] {uri} → no external node could reach it either");
                        return outcome;
                    }
                }

                this.log.Log($"[check-host] {uri} → timed out waiting for node results (inconclusive)");
                return CheckOutcome.Inconclusive;
            }
            catch (Exception ex)
            {
                this.log.Log($"[check-host] error for {uri} (inconclusive): {DiagnosticLogger.SummarizeException(ex)}");
                return CheckOutcome.Inconclusive;
            }
            finally
            {
                Throttle.Release();
            }
        }

        // Maps one check-result payload to the tri-state outcome: Online if any node is up,
        // Offline only once EVERY node has reported and none is up, otherwise Inconclusive
        // (still waiting, or nothing parseable).
        internal static CheckOutcome InterpretResults(string json)
        {
            bool anyUp;
            bool allReported;
            try
            {
                (anyUp, allReported) = ParseResults(json);
            }
            catch (JsonException)
            {
                return CheckOutcome.Inconclusive;
            }

            if (anyUp)
            {
                return CheckOutcome.Online;
            }

            return allReported ? CheckOutcome.Offline : CheckOutcome.Inconclusive;
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