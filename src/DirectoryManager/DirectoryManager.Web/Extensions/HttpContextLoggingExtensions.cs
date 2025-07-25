using DirectoryManager.Web.Models;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Extensions
{
    public static class HttpContextLoggingExtensions
    {
        public static bool ShouldLogIp(this HttpContext ctx)
        {
            var opts = ctx.RequestServices
                          .GetRequiredService<IOptions<AppSettings>>()
                          .Value;
            return opts.LogIpAddresses;
        }

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