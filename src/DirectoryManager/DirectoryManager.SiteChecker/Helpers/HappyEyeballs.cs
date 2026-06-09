using System.Net;
using System.Net.Sockets;

namespace DirectoryManager.SiteChecker.Helpers
{
    // Dual-stack connect that mirrors curl's Happy Eyeballs (RFC 8305):
    // resolve every A/AAAA, attempt them in parallel, the first socket to
    // connect wins, and the losers are cancelled and disposed. A black-holed
    // address family (e.g. broken IPv6 egress) simply loses the race rather
    // than stalling the connection until timeout.
    internal static class HappyEyeballs
    {
        public static async Task<Socket> ConnectAsync(
            string host,
            int port,
            TimeSpan perAddressTimeout,
            CancellationToken cancellationToken)
        {
            var addresses = IPAddress.TryParse(host, out var literal)
                ? new[] { literal }
                : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

            if (addresses.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var attempts = new List<Task<Socket>>(addresses.Length);
            foreach (var address in addresses)
            {
                attempts.Add(ConnectOneAsync(address, port, perAddressTimeout, cts.Token));
            }

            Socket? winner = null;
            Exception? lastError = null;

            // Drain every attempt so no task is orphaned. The first success is
            // kept and triggers cancellation of the rest; any straggler that
            // still succeeds afterward is disposed.
            while (attempts.Count > 0)
            {
                var finished = await Task.WhenAny(attempts).ConfigureAwait(false);
                attempts.Remove(finished);

                if (finished.Status == TaskStatus.RanToCompletion)
                {
                    if (winner == null)
                    {
                        winner = finished.Result;
                        cts.Cancel();
                    }
                    else
                    {
                        finished.Result.Dispose();
                    }
                }
                else
                {
                    lastError = finished.Exception?.InnerException ?? finished.Exception ?? lastError;
                }
            }

            if (winner != null)
            {
                return winner;
            }

            throw lastError ?? new SocketException((int)SocketError.HostUnreachable);
        }

        private static async Task<Socket> ConnectOneAsync(
            IPAddress address,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(timeout);
                await socket.ConnectAsync(new IPEndPoint(address, port), attemptCts.Token).ConfigureAwait(false);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}