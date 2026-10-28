using System.Net;
using System.Net.Sockets;

namespace DirectoryManager.Utilities.Helpers
{
    /// <summary>
    /// Single source of truth for "is this IP an internal / non-public address we must never
    /// connect to on behalf of user-supplied data" (SSRF protection). Used by the web app's
    /// outbound fetches and by the site checker, which follows listing URLs and their redirects.
    /// </summary>
    public static class NetworkAddressHelper
    {
        /// <summary>
        /// True when the address must not be contacted: loopback, private (RFC1918), CGNAT,
        /// link-local, unspecified, multicast/broadcast, IPv6 unique-local / site-local.
        /// IPv4-mapped IPv6 addresses are unwrapped and re-checked. Unknown families fail closed.
        /// </summary>
        /// <param name="ip">The address to classify.</param>
        /// <returns>True when the address is internal or otherwise disallowed.</returns>
        public static bool IsPrivateOrDisallowed(IPAddress? ip)
        {
            if (ip is null)
            {
                return true;
            }

            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                return b[0] == 0                                   // 0.0.0.0/8 (unspecified / "this host")
                    || b[0] == 10                                  // 10.0.0.0/8
                    || b[0] == 127                                 // 127.0.0.0/8 loopback
                    || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) // 100.64.0.0/10 CGNAT
                    || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 link-local / cloud metadata
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)  // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
                    || b[0] >= 224;                                // 224.0.0.0/4 multicast + 240/4 reserved + broadcast
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv4-mapped (::ffff:a.b.c.d) — unwrap so 192.168.x.x can't slip through as IPv6.
                if (ip.IsIPv4MappedToIPv6)
                {
                    return IsPrivateOrDisallowed(ip.MapToIPv4());
                }

                if (ip.Equals(IPAddress.IPv6Loopback) || ip.Equals(IPAddress.IPv6None))
                {
                    return true; // ::1 and :: (unspecified)
                }

                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
                {
                    return true;
                }

                var b = ip.GetAddressBytes();
                return (b[0] & 0xFE) == 0xFC; // fc00::/7 unique-local
            }

            // Unknown family — fail closed.
            return true;
        }
    }
}
