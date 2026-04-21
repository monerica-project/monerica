using System.Net.Sockets;
using System.Text;

namespace DirectoryManager.SiteChecker.Helpers
{
    public class DiagnosticLogger : IDisposable
    {
        private readonly object sync = new ();
        private readonly StreamWriter? writer;

        public DiagnosticLogger(string? filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                this.writer = new StreamWriter(stream) { AutoFlush = true };
                this.LogImportant($"===== DiagnosticLogger started: {filePath} =====");
            }
        }

        // Console only — noisy stuff like per-attempt status, headers, DNS results
        public void Log(string line)
        {
            var stamped = $"[{DateTime.UtcNow:HH:mm:ss}] {line}";
            Console.WriteLine(stamped);
        }

        // Console + file — only used when we need to record a false-negative-worthy event
        public void LogImportant(string line)
        {
            var stamped = $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}] {line}";
            Console.WriteLine(stamped);

            if (this.writer != null)
            {
                lock (this.sync)
                {
                    this.writer.WriteLine(stamped);
                    this.writer.Flush();
                    this.writer.BaseStream.Flush();
                }
            }
        }

        // Writes a compact summary of an offline verdict with exception details
        public void LogOfflineFailure(string uri, string scope, List<string> attemptSummaries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"OFFLINE {scope} {uri}");
            foreach (var a in attemptSummaries)
            {
                sb.AppendLine($"  {a}");
            }

            this.LogImportant(sb.ToString().TrimEnd());
        }

        public static string SummarizeException(Exception ex)
        {
            var parts = new List<string>();
            parts.Add($"{ex.GetType().Name}: {ex.Message}");

            if (ex is SocketException sockEx)
            {
                parts.Add($"SocketError={sockEx.SocketErrorCode}({(int)sockEx.SocketErrorCode})");
            }

            if (ex is HttpRequestException httpEx && httpEx.StatusCode != null)
            {
                parts.Add($"HttpStatus={httpEx.StatusCode}");
            }

            var inner = ex.InnerException;
            int depth = 1;
            while (inner != null && depth <= 3)
            {
                var innerPart = $"Inner{depth}={inner.GetType().Name}: {inner.Message}";
                if (inner is SocketException sEx)
                {
                    innerPart += $" (SocketError={sEx.SocketErrorCode}/{(int)sEx.SocketErrorCode})";
                }

                parts.Add(innerPart);
                inner = inner.InnerException;
                depth++;
            }

            return string.Join(" | ", parts);
        }

        public void Dispose()
        {
            this.writer?.Dispose();
        }
    }
}