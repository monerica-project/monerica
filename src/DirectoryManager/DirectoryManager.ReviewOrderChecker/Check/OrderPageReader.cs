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
            var shot = Path.Combine(Path.GetTempPath(), $"order-check-{Guid.NewGuid():N}.jpg");
            var psi = new ProcessStartInfo
            {
                FileName = this.options.PythonPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(this.options.RenderScriptPath);
            psi.ArgumentList.Add("--url");
            psi.ArgumentList.Add(url.AbsoluteUri);
            psi.ArgumentList.Add("--shot");
            psi.ArgumentList.Add(shot);
            if (OrderLinkFacts.IsOnion(url))
            {
                psi.ArgumentList.Add("--proxy");
                psi.ArgumentList.Add($"socks5://{this.options.TorHost}:{this.options.TorPort}");
            }

            if (!string.IsNullOrWhiteSpace(fill))
            {
                psi.ArgumentList.Add("--fill");
                psi.ArgumentList.Add(fill);
            }

            psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = this.options.BrowsersPath;
            psi.Environment["HOME"] = Path.GetTempPath();

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
                timeout.CancelAfter(TimeSpan.FromSeconds(this.options.RenderTimeoutSeconds));
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
                    catch (InvalidOperationException)
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

                return Parse(json, File.Exists(shot) ? await File.ReadAllBytesAsync(shot, ct) : null);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or JsonException)
            {
                return Failed("page reader error: " + ex.Message);
            }
            finally
            {
                try
                {
                    File.Delete(shot);
                }
                catch (IOException)
                {
                }
            }
        }

        internal static RenderedPage Parse(string json, byte[]? screenshot)
        {
            using var doc = JsonDocument.Parse(json[json.IndexOf('{')..]);
            var r = doc.RootElement;
            string? Str(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var inputs = r.TryGetProperty("inputs", out var i) && i.ValueKind == JsonValueKind.Array
                ? i.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                : new List<string>();
            return new RenderedPage(
                r.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True,
                Str("final_url"),
                r.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : null,
                Str("title"),
                Str("text") ?? string.Empty,
                inputs,
                Str("fill"),
                Str("error"),
                screenshot is { Length: > 0 and < 4_000_000 } ? screenshot : null);
        }

        private static RenderedPage Failed(string error) =>
            new (false, null, null, null, string.Empty, Array.Empty<string>(), null, error, null);

        private static string Last(string s, int max) => s.Length <= max ? s : s[^max..];
    }
}
