using DirectoryManager.Web.Models;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Extensions
{
    public static class HttpContextLoggingExtensions
    {
        /// <summary>
        /// True if the appsetting "Logging:TrafficLogging:LogIpAddresses" is set.
        /// </summary>
        public static bool ShouldLogIp(this HttpContext ctx)
        {
            var opts = ctx.RequestServices
                          .GetRequiredService<IOptions<AppSettings>>()
                          .Value;
            return opts.LogIpAddresses;
        }

        /// <summary>
        /// Returns the remote IP if & only if IP-logging is turned on; otherwise null.
        /// </summary>
        public static string? GetRemoteIpIfEnabled(this HttpContext ctx)
        {
            if (!ctx.ShouldLogIp())
            {
                return null;
            }

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    }
}