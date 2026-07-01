namespace DirectoryManager.ReviewModerator.Abstractions
{
    /// <summary>
    /// Resolves the correct <see cref="IOrderProofParser"/> for a submitted order URL by
    /// matching its host. Hosts are normalized (lower-cased, leading "www." stripped).
    /// </summary>
    public sealed class OrderProofParserRegistry
    {
        private readonly Dictionary<string, IOrderProofParser> byHost;

        public OrderProofParserRegistry(IEnumerable<IOrderProofParser> parsers)
        {
            this.byHost = new Dictionary<string, IOrderProofParser>(StringComparer.OrdinalIgnoreCase);

            foreach (var parser in parsers)
            {
                foreach (var host in parser.Hosts)
                {
                    this.byHost[Normalize(host)] = parser;
                }
            }
        }

        /// <summary>Returns the parser for the given order URL, or null if no domain matches.</summary>
        /// <returns></returns>
        public IOrderProofParser? Resolve(string? orderUrl)
        {
            if (string.IsNullOrWhiteSpace(orderUrl) ||
                !Uri.TryCreate(orderUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            return this.byHost.TryGetValue(Normalize(uri.Host), out var parser) ? parser : null;
        }

        private static string Normalize(string host)
        {
            host = host.Trim().ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        }
    }
}
