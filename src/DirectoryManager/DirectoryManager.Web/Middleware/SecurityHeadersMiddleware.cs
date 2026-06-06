namespace DirectoryManager.Web.Middleware
{
     public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate next;

    public const string Csp =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "frame-src 'none'; " +
        "form-action 'self'; " +
        "script-src 'none'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'";

    private const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), " +
        "magnetometer=(), microphone=(), payment=(), usb=()";

    public SecurityHeadersMiddleware(RequestDelegate next) => this.next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;

        // Indexer assignment overwrites rather than appends, so even if a
        // downstream layer also sets these we end up with exactly one value.
        h["Content-Security-Policy"] = Csp;
        h["X-Content-Type-Options"]  = "nosniff";
        h["X-Frame-Options"]         = "DENY";
        h["Referrer-Policy"]         = "no-referrer";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Permissions-Policy"]      = PermissionsPolicy;

        if (context.Request.IsHttps)
        {
            h["Strict-Transport-Security"] = "max-age=31536000";
        }

        return this.next(context);
    }
}
}