using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectoryManager.Services.Interfaces;

namespace DirectoryManager.Services.Implementations
{
    public sealed class DomainRegistrationDateService : IDomainRegistrationDateService
    {
        private const string IanaRdapBootstrapUrl = "https://data.iana.org/rdap/dns.json";
        private const string IanaWhoisServer = "whois.iana.org";

        // StyleCop-friendly (no target-typed new, no underscores)
        private static readonly IdnMapping IdnMappingInstance = new IdnMapping();

        private static readonly Regex CreationDateRegex = new Regex(
            @"(?im)^(?:Creation Date|Created On|Created|Registered On|Registration Date|Domain Create Date|Domain Registration Date)\s*:\s*(.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Minimal public-suffix help without extra packages (covers common multi-part suffixes)
        private static readonly HashSet<string> CommonTwoPartPublicSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "co.uk", "org.uk", "ac.uk", "gov.uk", "net.uk", "sch.uk",
            "com.au", "net.au", "org.au", "edu.au", "gov.au", "id.au", "asn.au",
            "co.nz", "org.nz", "govt.nz",
            "co.jp", "ne.jp", "or.jp", "go.jp", "ac.jp",
            "com.br", "net.br", "org.br",
        };

        private readonly HttpClient httpClient;
        private readonly Lazy<Task<RdapBootstrap>> rdapBootstrapLazy;

        public DomainRegistrationDateService(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.rdapBootstrapLazy = new Lazy<Task<RdapBootstrap>>(this.LoadRdapBootstrapAsync);
        }

        public async Task<DateOnly?> GetDomainRegistrationDateAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            if (!TryGetHost(url, out string host))
            {
                return null;
            }

            host = NormalizeHost(host);

            // Skip onion/i2p explicitly
            if (IsOnionOrI2p(host))
            {
                return null;
            }

            // Skip IP hosts
            if (IPAddress.TryParse(host, out _))
            {
                return null;
            }

            host = ToAsciiHost(host);

            string? registrableDomain = this.GetRegistrableDomain(host);
            if (string.IsNullOrWhiteSpace(registrableDomain))
            {
                return null;
            }

            DateOnly? rdap = await this.TryGetFromRdapAsync(registrableDomain, cancellationToken).ConfigureAwait(false);
            if (rdap is not null)
            {
                return rdap;
            }

            DateOnly? whois = await this.TryGetFromWhoisAsync(registrableDomain, cancellationToken).ConfigureAwait(false);
            return whois;
        }

        private static bool TryGetHost(string url, out string host)
        {
            host = string.Empty;

            // Accept URLs without scheme
            if (!url.Contains("://", StringComparison.Ordinal))
            {
                url = "https://" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            host = uri.Host;
            return !string.IsNullOrWhiteSpace(host);
        }

        private static string NormalizeHost(string host)
        {
            return host.Trim().TrimEnd('.').ToLowerInvariant();
        }

        private static bool IsOnionOrI2p(string host)
        {
            return host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".i2p", StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAsciiHost(string host)
        {
            try
            {
                return IdnMappingInstance.GetAscii(host);
            }
            catch
            {
                return host;
            }
        }

        private static string? GetTld(string registrableDomain)
        {
            string[] parts = registrableDomain.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? parts[parts.Length - 1] : null;
        }

        private string? GetRegistrableDomain(string asciiHost)
        {
            // Very small heuristic:
            // - if ends with known 2-part public suffix (co.uk, com.au, etc.), use last 3 labels
            // - else use last 2 labels
            // This avoids extra packages and fixes your CS0246 errors.
            string[] parts = asciiHost.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            string last2 = parts[parts.Length - 2] + "." + parts[parts.Length - 1];

            if (CommonTwoPartPublicSuffixes.Contains(last2))
            {
                if (parts.Length < 3)
                {
                    // e.g., "co.uk" alone
                    return asciiHost;
                }

                return parts[parts.Length - 3] + "." + last2;
            }

            return last2;
        }

        private async Task<RdapBootstrap> LoadRdapBootstrapAsync()
        {
            using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, IanaRdapBootstrapUrl);
            req.Headers.Accept.ParseAdd("application/json");

            using HttpResponseMessage resp = await this.httpClient
                .SendAsync(req, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);

            resp.EnsureSuccessStatusCode();

            await using Stream stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (doc.RootElement.TryGetProperty("services", out JsonElement services) &&
                services.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement service in services.EnumerateArray())
                {
                    if (service.ValueKind != JsonValueKind.Array || service.GetArrayLength() < 2)
                    {
                        continue;
                    }

                    JsonElement tlds = service[0];
                    JsonElement urls = service[1];

                    if (tlds.ValueKind != JsonValueKind.Array || urls.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    List<string> urlList = urls.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString() ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(EnsureTrailingSlash)
                        .ToList();

                    if (urlList.Count == 0)
                    {
                        continue;
                    }

                    foreach (JsonElement tldEl in tlds.EnumerateArray())
                    {
                        if (tldEl.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string? tld = tldEl.GetString();
                        if (string.IsNullOrWhiteSpace(tld))
                        {
                            continue;
                        }

                        map[tld] = urlList;
                    }
                }
            }

            return new RdapBootstrap(map);
        }

        private async Task<DateOnly?> TryGetFromRdapAsync(string registrableDomain, CancellationToken cancellationToken)
        {
            RdapBootstrap bootstrap = await this.rdapBootstrapLazy.Value.ConfigureAwait(false);

            string? tld = GetTld(registrableDomain);
            if (tld is null)
            {
                return null;
            }

            if (!bootstrap.TryGetUrlsForTld(tld, out List<string>? baseUrls) || baseUrls is null || baseUrls.Count == 0)
            {
                return null;
            }

            foreach (string baseUrl in baseUrls)
            {
                try
                {
                    Uri endpoint = new Uri(new Uri(baseUrl), "domain/" + registrableDomain);

                    using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, endpoint);
                    req.Headers.Accept.ParseAdd("application/rdap+json");

                    using HttpResponseMessage resp = await this.httpClient
                        .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);

                    if (!resp.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    await using Stream stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!doc.RootElement.TryGetProperty("events", out JsonElement events) ||
                        events.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    DateTimeOffset? best = null;

                    foreach (JsonElement ev in events.EnumerateArray())
                    {
                        if (ev.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        string? action = ev.TryGetProperty("eventAction", out JsonElement actionEl) && actionEl.ValueKind == JsonValueKind.String
                            ? actionEl.GetString()
                            : null;

                        if (!string.Equals(action, "registration", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string? dateStr = ev.TryGetProperty("eventDate", out JsonElement dateEl) && dateEl.ValueKind == JsonValueKind.String
                            ? dateEl.GetString()
                            : null;

                        if (TryParseDateTimeOffset(dateStr, out DateTimeOffset dto))
                        {
                            best = best is null ? dto : (dto < best ? dto : best);
                        }
                    }

                    if (best is null)
                    {
                        return null;
                    }

                    return DateOnly.FromDateTime(best.Value.UtcDateTime);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Try next base URL
                }
            }

            return null;
        }

        private static bool TryParseDateTimeOffset(string? value, out DateTimeOffset dto)
        {
            dto = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dto);
        }

        private static string EnsureTrailingSlash(string s)
        {
            return s.EndsWith("/", StringComparison.Ordinal) ? s : (s + "/");
        }

        private static string? ParseWhoisServerFromIana(string ianaResponse)
        {
            foreach (string line in ianaResponse.Split('\n'))
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("whois:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("whois:".Length).Trim();
                }

                if (trimmed.StartsWith("refer:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("refer:".Length).Trim();
                }
            }

            return null;
        }

        private static DateOnly? TryParseCreationDateFromWhois(string whoisText)
        {
            Match match = CreationDateRegex.Match(whoisText);
            if (!match.Success)
            {
                return null;
            }

            string raw = match.Groups[1].Value.Trim();

            // Remove trailing parenthetical notes like "(UTC)" if present
            int paren = raw.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0)
            {
                raw = raw.Substring(0, paren).Trim();
            }

            // First try liberal parse
            if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset dto))
            {
                return DateOnly.FromDateTime(dto.UtcDateTime);
            }

            // Then try strict formats
            string[] formats =
            {
                "yyyy-MM-dd",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:sszzz",
                "yyyy-MM-ddTHH:mm:ss.fffzzz",
                "dd-MMM-yyyy",
                "yyyy.MM.dd",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss 'UTC'",
            };

            foreach (string f in formats)
            {
                if (DateTime.TryParseExact(
                    raw,
                    f,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime dt))
                {
                    return DateOnly.FromDateTime(dt);
                }
            }

            return null;
        }

        private static async Task<string> QueryWhoisAsync(string server, string query, CancellationToken cancellationToken)
        {
            using TcpClient tcpClient = new TcpClient();

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            // ConnectAsync has no CT on some TFMs; use WaitAsync
            await tcpClient.ConnectAsync(server, 43).WaitAsync(timeoutCts.Token).ConfigureAwait(false);

            await using NetworkStream stream = tcpClient.GetStream();

            byte[] requestBytes = Encoding.ASCII.GetBytes(query + "\r\n");
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, timeoutCts.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

            using MemoryStream ms = new MemoryStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);

            try
            {
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, timeoutCts.Token).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private async Task<DateOnly?> TryGetFromWhoisAsync(string registrableDomain, CancellationToken cancellationToken)
        {
            string? tld = GetTld(registrableDomain);
            if (tld is null)
            {
                return null;
            }

            string ianaResponse = await QueryWhoisAsync(IanaWhoisServer, tld, cancellationToken).ConfigureAwait(false);
            string? whoisServer = ParseWhoisServerFromIana(ianaResponse);

            if (string.IsNullOrWhiteSpace(whoisServer))
            {
                return null;
            }

            string whoisResponse = await QueryWhoisAsync(whoisServer, registrableDomain, cancellationToken).ConfigureAwait(false);
            return TryParseCreationDateFromWhois(whoisResponse);
        }

        private sealed class RdapBootstrap
        {
            private readonly Dictionary<string, List<string>> tldToUrls;

            public RdapBootstrap(Dictionary<string, List<string>> tldToUrls)
            {
                this.tldToUrls = tldToUrls;
            }

            public bool TryGetUrlsForTld(string tld, out List<string>? urls)
            {
                return this.tldToUrls.TryGetValue(tld, out urls);
            }
        }
    }
}
