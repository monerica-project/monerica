using System.Diagnostics;
using System.Net;
using System.Text.Json;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.SubmissionReviewer.Text;

namespace DirectoryManager.SubmissionReviewer.Fetching
{
    /// <summary>What a real browser showed for a page.</summary>
    public sealed record RenderedText(string? Title, string Text);

    public interface IPageRenderer
    {
        /// <summary>Opens the page in a headless browser and returns its visible text; null when it could not.</summary>
        Task<RenderedText?> RenderAsync(Uri url, CancellationToken ct);
    }

    /// <summary>
    /// Second look at a page the plain fetch could not read (built by JavaScript, or refused to a
    /// script). Starts the same locked-down headless-browser runner the review order checker uses,
    /// as a separate user and process, with none of this job's environment.
    /// </summary>
    public sealed class PageRenderer : IPageRenderer
    {
        /// <summary>Below this much visible text a page counts as unreadable by the plain fetch.</summary>
        public const int ThinTextChars = 200;

        private readonly ReviewerOptions options;

        public PageRenderer(ReviewerOptions options) => this.options = options;

        /// <summary>True when the plain fetch got nothing a reviewer could read.</summary>
        public static bool NeedsRender(FetchResult fetched)
        {
            if (fetched.Html == null)
            {
                return fetched.Blocked;
            }

            if (fetched.Blocked)
            {
                return true;
            }

            return HtmlText.Extract(fetched.Html).Body.Trim().Length < ThinTextChars;
        }

        /// <summary>Adds the browser's text to what was fetched, keeping the original markup for the other checks.</summary>
        public static FetchResult Merge(FetchResult fetched, RenderedText rendered)
        {
            var block = "<div data-rendered-by-browser=\"1\"><p>"
                + WebUtility.HtmlEncode(rendered.Text).Replace("\n", "</p><p>", StringComparison.Ordinal)
                + "</p></div>";

            string html;
            if (string.IsNullOrWhiteSpace(fetched.Html) || fetched.Blocked)
            {
                html = "<html><head><title>" + WebUtility.HtmlEncode(rendered.Title ?? string.Empty) + "</title></head><body>" + block + "</body></html>";
            }
            else
            {
                var at = fetched.Html.LastIndexOf("</body", StringComparison.OrdinalIgnoreCase);
                html = at >= 0 ? fetched.Html.Insert(at, block) : fetched.Html + block;
            }

            var status = fetched.Blocked || fetched.StatusCode == null ? 200 : fetched.StatusCode;
            return fetched with { Reachable = true, StatusCode = status, Html = html, Error = null };
        }

        public async Task<RenderedText?> RenderAsync(Uri url, CancellationToken ct)
        {
            if (!this.options.RenderEnabled
                || string.IsNullOrWhiteSpace(this.options.RenderRunAsUser)
                || !File.Exists(this.options.RenderRunnerPath)
                || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            var psi = new ProcessStartInfo
            {
                FileName = this.options.SudoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            // The page is untrusted: the browser gets none of this job's environment (AI key, database).
            psi.Environment.Clear();
            psi.Environment["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";
            psi.Environment["LANG"] = "C.UTF-8";
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(this.options.RenderRunAsUser.Trim());
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(this.options.RenderRunnerPath);
            psi.ArgumentList.Add("--url=" + url.AbsoluteUri);
            if (UrlNormalizer.IsOnion(url.AbsoluteUri))
            {
                psi.ArgumentList.Add($"--proxy=socks5://{this.options.TorHost}:{this.options.TorPort}");
            }

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    return null;
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
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                    {
                    }

                    return null;
                }

                await stderr;
                return Parse(await stdout);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or JsonException or FormatException or ArgumentException)
            {
                Console.WriteLine("Page renderer failed: " + ex.Message);
                return null;
            }
        }

        internal static RenderedText? Parse(string? json)
        {
            var start = json?.IndexOf('{') ?? -1;
            if (json == null || start < 0)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json[start..]);
            var r = doc.RootElement;
            string? Str(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var ok = r.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
            var text = (Str("text") ?? string.Empty).Trim();
            if (!ok || text.Length == 0)
            {
                return null;
            }

            const int MaxText = 20000;
            return new RenderedText(Str("title"), text.Length <= MaxText ? text : text[..MaxText]);
        }
    }
}
