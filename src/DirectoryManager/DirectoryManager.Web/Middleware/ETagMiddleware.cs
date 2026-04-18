using System.Security.Cryptography;

namespace DirectoryManager.Web.Middleware
{
    public class ETagMiddleware
    {
        private readonly RequestDelegate next;

        public ETagMiddleware(RequestDelegate next) => this.next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // only GET/HEAD are candidates
            if (!(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
            {
                await this.next(context);
                return;
            }

            // allow callers to bypass (we’ll do this for the CSV route)
            if (context.Items.TryGetValue("SkipETag", out var skip) && skip is true)
            {
                await this.next(context);
                return;
            }

            // don’t buffer obvious downloads or big bodies
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Contains("/sponsoredlistinginvoice/download-accountant-csv", StringComparison.OrdinalIgnoreCase))
            {
                await this.next(context);
                return;
            }

            var original = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await this.next(context);

                // non-200 -> just pass through
                if (context.Response.StatusCode != StatusCodes.Status200OK)
                {
                    buffer.Position = 0;
                    await buffer.CopyToAsync(original);
                    return;
                }

                // skip if attachment / typical download types / too large
                var cd = context.Response.Headers["Content-Disposition"].ToString();
                var isAttachment = cd.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0;

                var ct = context.Response.ContentType ?? "";
                var isDownloadType =
                    ct.StartsWith("text/csv", StringComparison.OrdinalIgnoreCase) ||
                    (ct.StartsWith("application/", StringComparison.OrdinalIgnoreCase) &&
                    (ct.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
                     ct.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) ||
                     ct.Contains("pdf", StringComparison.OrdinalIgnoreCase)));

                const long MaxEtageableBytes = 2L * 1024 * 1024; // cap to 2MB
                if (isAttachment || isDownloadType || buffer.Length > MaxEtageableBytes)
                {
                    buffer.Position = 0;
                    await buffer.CopyToAsync(original);
                    return;
                }

                // compute ETag (no ToArray())
                buffer.Position = 0;
                string eTag;
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(buffer);
                    eTag = $"\"{BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}\"";
                }

                context.Response.Headers["ETag"] = eTag;

                if (context.Request.Headers.TryGetValue("If-None-Match", out var inm) && inm == eTag)
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.Headers["Content-Length"] = "0";
                    return;
                }

                buffer.Position = 0;
                await buffer.CopyToAsync(original);
            }
            finally
            {
                // CRITICAL: restore original stream
                context.Response.Body = original;
            }
        }
    }
}