using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace DirectoryManager.Web.Helpers
{
    /// <summary>
    /// Central SSRF guard. Single source of truth for "is this IP a private / internal /
    /// otherwise-disallowed target", plus a <see cref="SocketsHttpHandler"/> ConnectCallback
    /// that pins the connection to a validated IP.
    ///
    /// Pinning closes the DNS-rebinding / TOCTOU window: up-front host validation and the
    /// actual socket connect now resolve and validate the SAME address set, so an attacker
    /// cannot answer a public IP at validation time and a private IP at connect time.
    /// </summary>
    public static class PrivateNetworkGuard
    {
        /// <summary>
        /// Returns true when the address must not be contacted (loopback, RFC1918, CGNAT,
        /// link-local, IPv6 ULA, etc.). IPv4-mapped IPv6 addresses are unwrapped and re-checked.
        /// Unknown address families are treated as disallowed (fail closed).
        /// </summary>
        /// <returns></returns>
        public static bool IsPrivateOrDisallowedIp(IPAddress ip)
            => DirectoryManager.Utilities.Helpers.NetworkAddressHelper.IsPrivateOrDisallowed(ip);

        /// <summary>
        /// ConnectCallback for <see cref="SocketsHttpHandler"/>. Resolves the requested host,
        /// rejects the connection if ANY resolved address is private/disallowed, then connects
        /// only to that validated address set — so the IP that is validated is the exact IP used.
        /// </summary>
        /// <returns></returns>
        public static async ValueTask<Stream> SafeConnectAsync(
            SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            var host = context.DnsEndPoint.Host;
            var port = context.DnsEndPoint.Port;

            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var literal))
            {
                addresses = new[] { literal };
            }
            else
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken)
                                     .ConfigureAwait(false);
            }

            if (addresses is null || addresses.Length == 0)
            {
                throw new HttpRequestException($"Could not resolve host '{host}'.");
            }

            // Strict: if the host resolves to ANY disallowed address, refuse the whole connection.
            foreach (var addr in addresses)
            {
                if (IsPrivateOrDisallowedIp(addr))
                {
                    throw new HttpRequestException(
                        $"Blocked connection to private/disallowed address for host '{host}'.");
                }
            }

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
