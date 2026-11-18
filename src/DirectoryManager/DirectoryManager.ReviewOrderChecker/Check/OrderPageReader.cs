using System.Diagnostics;
using System.Text.Json;

namespace DirectoryManager.ReviewOrderChecker.Check
{
    public sealed record RenderedPage(
        bool Ok, string? FinalUrl, int? Status, string? Title, string Text, IReadOnlyList<string> Inputs, string? FillResult, string? Error, byte[]? Screenshot);

    public interface IOrderPageReader
    {
        /// <summary>Opens the page like a visitor; with <paramref name="fill"/>, types that one value into the page's lookup box first.</summary>
        Task<RenderedPage> ReadAsync(Uri url, string? fill, CancellationToken ct);
    }

    /// <summary>
    /// Runs the headless-browser script (Render/render.py) as a separate process, so a slow or
    /// broken page can be killed without taking the job down. Onion links go through Tor.
    /// </summary>
    public sealed class OrderPageReader : IOrderPageReader
    {
        private readonly OrderCheckOptions options;

        public OrderPageReader(OrderCheckOptions options)
        {
            this.options = options;
        }

        public async Task<RenderedPage> ReadAsync(Uri url, string? fill, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            // The page is hostile until proven otherwise, so the browser gets none of this job's
            // environment (it holds the AI key and the database connection).
            psi.Environment.Clear();
            psi.Environment["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";
            psi.Environment["LANG"] = "C.UTF-8";

            var asOtherUser = !string.IsNullOrWhiteSpace(this.options.RunAsUser);
            if (asOtherUser)
            {
                // Server: a separate user that the firewall keeps away from this machine and from
                // private networks. The runner script sets the browser path, a time limit and the
                // script to run; sudo is allowed to start exactly that one script.
                psi.FileName = this.options.SudoPath;
                psi.ArgumentList.Add("-n");
                psi.ArgumentList.Add("-u");
                psi.ArgumentList.Add(this.options.RunAsUser.Trim());
                psi.ArgumentList.Add("--");
                psi.ArgumentList.Add(this.options.RunnerPath);
            }
            else
            {
                psi.FileName = this.options.PythonPath;
                psi.ArgumentList.Add(this.options.RenderScriptPath);
                psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = this.options.BrowsersPath;
                psi.Environment["HOME"] = Path.GetTempPath();
            }

            psi.ArgumentList.Add("--url=" + url.AbsoluteUri);
            psi.ArgumentList.Add("--shot-inline");
            if (OrderLinkFacts.IsOnion(url))
            {
                psi.ArgumentList.Add($"--proxy=socks5://{this.options.TorHost}:{this.options.TorPort}");
            }

            if (!string.IsNullOrWhiteSpace(fill))
            {
                // "--fill=value" form: a value that starts with "-" is still read as the value.
                psi.ArgumentList.Add("--fill=" + fill);
            }

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    return Failed("could not start the page reader");
                }

                var stdout = process.StandardOutput.ReadToEndAsync(ct);
                var stderr = process.StandardError.ReadToEndAsync(ct);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

                // Run as another user, this job cannot stop the browser itself; the runner script
                // carries its own time limit, so wait a little longer than that before giving up.
                timeout.CancelAfter(TimeSpan.FromSeconds(this.options.RenderTimeoutSeconds + (asOtherUser ? 40 : 0)));
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                    {
                    }

                    return Failed("the page took too long to open");
                }

                var json = await stdout;
                if (string.IsNullOrWhiteSpace(json))
                {
                    var err = (await stderr).Trim();
                    return Failed("the page reader gave no result" + (err.Length > 0 ? ": " + Last(err, 200) : string.Empty));
                }

                return Parse(json, null);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or JsonException or FormatException or ArgumentException)
            {
                return Failed("page reader error: " + ex.Message);
            }
        }

        internal static RenderedPage Parse(string json, byte[]? screenshot)
        {
            var start = json.IndexOf('{');
            if (start < 0)
            {
                throw new JsonException("the page reader did not return JSON");
            }

            using var doc = JsonDocument.Parse(json[start..]);
            var r = doc.RootElement;
            string? Str(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var inputs = r.TryGetProperty("inputs", out var i) && i.ValueKind == JsonValueKind.Array
                ? i.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                : new List<string>();

            // The screenshot comes back inside the JSON (the browser may run as another user,
            // which could not hand over a file).
            if (screenshot == null && Str("shot_b64") is { Length: > 0 and < 6_000_000 } b64)
            {
                screenshot = Convert.FromBase64String(b64);
            }

            return new RenderedPage(
                r.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True,
                Truncate(Str("final_url"), 500),
                r.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : null,
                Str("title"),
                Str("text") ?? string.Empty,
                inputs,
                Str("fill"),
                Str("error"),
                screenshot is { Length: > 0 and < 4_000_000 } ? screenshot : null);
        }

        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max];

        private static RenderedPage Failed(string error) =>
            new (false, null, null, null, string.Empty, Array.Empty<string>(), null, error, null);

        private static string Last(string s, int max) => s.Length <= max ? s : s[^max..];
    }
}
