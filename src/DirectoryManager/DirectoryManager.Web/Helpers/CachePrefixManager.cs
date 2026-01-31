using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;

namespace DirectoryManager.Web.Helpers
{
    public static class CachePrefixManager
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> PrefixTokens = new ();

        public static IChangeToken GetToken(string prefix)
        {
            var cts = PrefixTokens.GetOrAdd(prefix, _ => new CancellationTokenSource());
            return new CancellationChangeToken(cts.Token);
        }

        public static void ExpirePrefix(string prefix)
        {
            if (PrefixTokens.TryRemove(prefix, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
    }
}
