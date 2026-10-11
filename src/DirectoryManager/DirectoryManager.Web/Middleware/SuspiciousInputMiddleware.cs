using DirectoryManager.Web.Helpers;

namespace DirectoryManager.Web.Middleware
{
    // Refuses requests whose query string carries SQL-injection / script-injection probes
    // (see SuspiciousInputGuard). Runs before routing, static files and status-code pages so
    // the request never reaches a controller, the search log or the database, and the caller
    // gets a bare 400 rather than a rendered page.
    public class SuspiciousInputMiddleware
    {
        private const string Body = "Bad request.";

        private readonly RequestDelegate next;
        private readonly ILogger<SuspiciousInputMiddleware> logger;

        public SuspiciousInputMiddleware(RequestDelegate next, ILogger<SuspiciousInputMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var violation = FindViolation(context.Request);
            if (violation is null)
            {
                await this.next(context);
                return;
            }

            // Path only: the offending value is hostile input and is not written to the log.
            this.logger.LogWarning(
                "Rejected request with suspicious input ({Violation}) on {Path}",
                violation,
                context.Request.Path.Value);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Robots-Tag"] = "noindex";
            await context.Response.WriteAsync(Body);
        }

        private static string? FindViolation(HttpRequest request)
        {
            if (!request.QueryString.HasValue)
            {
                return null;
            }

            if (request.QueryString.Value!.Length > 4096)
            {
                return "query-too-long";
            }

            foreach (var pair in request.Query)
            {
                var hit = SuspiciousInputGuard.FindViolation(pair.Key);
                if (hit is not null)
                {
                    return hit;
                }

                foreach (var value in pair.Value)
                {
                    hit = SuspiciousInputGuard.FindViolation(value);
                    if (hit is not null)
                    {
                        return hit;
                    }
                }
            }

            return null;
        }
    }
}
