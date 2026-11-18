using System.Net;
using System.Security.Cryptography;
using System.Text;
using DirectoryManager.Web.Models.StaticPublishing;
using Microsoft.AspNetCore.Http;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Decides whether a request may use the internal static-publish endpoints. All must hold:
    /// the endpoints are configured; the request arrived on the loopback-only internal port (nginx
    /// and Tor proxy to the public port, so nothing from outside can reach it); it comes from a
    /// loopback address; it carries no proxy headers (anything nginx/Bunny relayed has them); and
    /// it carries the shared secret (compared in constant time). Pure, so it is unit-tested.
    /// </summary>
    public static class InternalEndpointGuard
    {
        private static readonly string[] ProxyHeaders =
        {
            "X-Forwarded-For", "X-Original-For", "X-Real-IP", "X-Forwarded-Proto", "X-Original-Proto",
            "X-Forwarded-Host", "Forwarded", "CDN-RequestId", "CDN-ServerZone",
        };

        public static bool IsAllowed(int localPort, IPAddress? remoteIp, IHeaderDictionary headers, StaticPublishOptions options, out string reason)
        {
            if (!options.InternalEndpointEnabled)
            {
                reason = "internal endpoint not configured";
                return false;
            }

            if (localPort != options.InternalPort)
            {
                reason = "not on the internal port";
                return false;
            }

            if (remoteIp == null || !IPAddress.IsLoopback(remoteIp))
            {
                reason = "not from loopback";
                return false;
            }

            foreach (var h in ProxyHeaders)
            {
                if (headers.ContainsKey(h))
                {
                    reason = "request came through a proxy";
                    return false;
                }
            }

            var sent = headers.TryGetValue(options.SecretHeaderName, out var v) ? v.ToString() : string.Empty;
            if (!SecretMatches(sent, options.InternalSecret))
            {
                reason = "missing or wrong secret";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool SecretMatches(string? sent, string? expected)
        {
            if (string.IsNullOrEmpty(sent) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            var a = SHA256.HashData(Encoding.UTF8.GetBytes(sent));
            var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
