using System.Security.Cryptography;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Middleware
{
    /// <summary>
    /// Emits a Content-Security-Policy (with a per-request nonce) plus a set of
    /// hardening headers on every HTML response. CSP is the backstop that turns a
    /// stray @Html.Raw or an injected tag into a non-event: injected &lt;script&gt;,
    /// inline on*= handlers, and javascript: URIs are all refused by the browser.
    ///
    /// Notes specific to this site:
    ///   - upgrade-insecure-requests is intentionally OMITTED so the .onion (http) mirror keeps working.
    ///   - style-src allows 'unsafe-inline' because admin CSS snippets inject inline styles.
    ///   - script-src is 'self' + a per-request nonce only (no 'unsafe-inline'); inline scripts
    ///     must carry nonce="@Context.Items[SecurityHeadersMiddleware.NonceKey]".
    ///   - ADMIN SURFACE: any endpoint carrying [Authorize] gets script-src 'none' and is ALWAYS
    ///     enforced (never report-only). The authenticated admin area runs with zero JavaScript.
    ///
    /// Views read the nonce via HttpContext.Items[NonceKey].
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        public const string NonceKey = "CspNonce";

        private readonly RequestDelegate next;
        private readonly SecurityHeadersOptions options;

        public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options)
        {
            this.next = next;
            this.options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 16 random bytes -> base64. New value every request.
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;

            // Routing has already run (this middleware sits after UseRouting), so the
            // matched endpoint and its [Authorize]/[AllowAnonymous] metadata are available.
            var forceNoScript = IsAuthorizedAdminEndpoint(context);

            // Headers must be written before the body starts streaming.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                if (!headers.ContainsKey("Content-Security-Policy") &&
                    !headers.ContainsKey("Content-Security-Policy-Report-Only"))
                {
                    if (forceNoScript)
                    {
                        // Admin: force no JavaScript, and ENFORCE regardless of the report-only setting.
                        headers["Content-Security-Policy"] =
                            ContentSecurityPolicyBuilder.Build(this.options, nonce, noScript: true);
                    }
                    else
                    {
                        var cspHeaderName = this.options.ContentSecurityPolicyReportOnly
                            ? "Content-Security-Policy-Report-Only"
                            : "Content-Security-Policy";

                        headers[cspHeaderName] = ContentSecurityPolicyBuilder.Build(this.options, nonce);
                    }
                }

                SetIfAbsent(headers, "X-Content-Type-Options", "nosniff");
                SetIfAbsent(headers, "X-Frame-Options", "DENY");
                SetIfAbsent(headers, "Referrer-Policy", "no-referrer");
                SetIfAbsent(headers, "Cross-Origin-Opener-Policy", "same-origin");
                SetIfAbsent(
                    headers,
                    "Permissions-Policy",
                    "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

                return Task.CompletedTask;
            });

            await this.next(context);
        }

        /// <summary>
        /// True when the matched endpoint requires authorization (an admin page on this site)
        /// and is not explicitly anonymous. Public pages return false and keep the standard policy.
        /// </summary>
        private static bool IsAuthorizedAdminEndpoint(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null)
            {
                return false;
            }

            // An [AllowAnonymous] on the action/controller wins over [Authorize].
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                return false;
            }

            return endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null;
        }

        private static void SetIfAbsent(IHeaderDictionary headers, string name, string value)
        {
            if (!headers.ContainsKey(name))
            {
                headers[name] = value;
            }
        }
    }
}