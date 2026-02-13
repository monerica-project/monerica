using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DirectoryManager.Console.Models;
using DirectoryManager.Console.Services;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models; // DirectoryEntry
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;
using Nager.PublicSuffix.RuleProviders.CacheProviders;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName)
    .Build();

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(StringConstants.DefaultConnection)))
    .AddTransient<IApplicationDbContext, ApplicationDbContext>()
    .AddTransient<IDbInitializer, DbInitializer>()
    .AddTransient<IDirectoryEntryRepository, DirectoryEntryRepository>()
    .AddTransient<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>()
    .AddTransient<ICategoryRepository, CategoryRepository>()
    .AddTransient<IExcludeUserAgentRepository, ExcludeUserAgentRepository>()
    .AddTransient<ITagRepository, TagRepository>()
    .AddTransient<IDirectoryEntryTagRepository, DirectoryEntryTagRepository>()
    .AddScoped<IAITagService, AITagService>()
    .BuildServiceProvider();

Console.WriteLine("Type 1 for user agent loaded");
Console.WriteLine("Type 2 for AI tags");
Console.WriteLine("Type 3 to group sites by A-record IP (root-domain consolidated + show multi-IP roots only)");
var choice = Console.ReadLine();

if (choice == "1")
{
    var fileDir = Directory.GetCurrentDirectory() + @"\Content";
    var json = File.ReadAllText(Path.Combine(fileDir, "crawler-user-agents.json"), Encoding.UTF8);
    var jsonData = JsonConvert.DeserializeObject<List<UserAgentModel>>(json);

    if (jsonData == null)
    {
        return;
    }

    var userAgentRepository = serviceProvider.GetService<IExcludeUserAgentRepository>()
        ?? throw new InvalidOperationException("The IExcludeUserAgentRepository service is not registered.");

    foreach (var instance in jsonData)
    {
        if (instance.Instances == null)
        {
            continue;
        }

        foreach (var userAgent in instance.Instances)
        {
            if (userAgentRepository.Exists(userAgent))
            {
                continue;
            }

            userAgentRepository.Create(new DirectoryManager.Data.Models.ExcludeUserAgent()
            {
                UserAgent = userAgent
            });
        }
    }
}
else if (choice == "2")
{
    var apiKey = "";

    var openAi = new OpenAIClient(apiKey);
    var directoryEntryRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
    var tagRepo = serviceProvider.GetRequiredService<ITagRepository>();
    var entryTagRepo = serviceProvider.GetRequiredService<IDirectoryEntryTagRepository>();

    var chat = openAi.GetChatClient("gpt-4.1");
    var messages = new List<ChatMessage>
    {
        new SystemChatMessage("You are a helpful assistant."),
        new UserChatMessage("Give me 3 comma-separated tags for: Monero privacy-focused wallet")
    };

    var aiTagService = new AITagService(openAi, directoryEntryRepo, tagRepo, entryTagRepo);

    await aiTagService.GenerateTagsForAllEntriesAsync();

    var response = await chat.CompleteChatAsync(messages).ConfigureAwait(false);
    var tags = response.Value.Content[0].Text;
}
else if (choice == "3")
{
    // ✅ CONFIG
    var dnsTimeout = TimeSpan.FromSeconds(6);
    var maxAttemptsPerHost = 2;
    var retryDelay = TimeSpan.FromMilliseconds(250);

    // ✅ Public Suffix List parser (Nager.PublicSuffix v3.x)
    // Recommended console setup: CachedHttpRuleProvider + LocalFileSystemCacheProvider
    Console.WriteLine("Initializing Public Suffix List (PSL) rule provider...");
    using var httpClient = new HttpClient();
    var cacheProvider = new LocalFileSystemCacheProvider();
    var ruleProvider = new CachedHttpRuleProvider(cacheProvider, httpClient);

    await ruleProvider.BuildAsync(); // REQUIRED before parsing, otherwise DomainDataStructure is not available
    var domainParser = new DomainParser(ruleProvider);

    // ✅ DNS cache so repeated hosts don't re-resolve
    var dnsCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Loading DirectoryEntries from DB...");
    var db = serviceProvider.GetRequiredService<ApplicationDbContext>();

    var allEntries = await db.Set<DirectoryEntry>()
        .AsNoTracking()
        .ToListAsync();

    Console.WriteLine($"Loaded {allEntries.Count:n0} entries.");

    var entries = allEntries
        .Where(e => !IsRemovedEntry(e))
        .ToList();

    Console.WriteLine($"Non-removed entries: {entries.Count:n0}");
    Console.WriteLine();

    // ip -> set of ROOT domains (consolidated)
    var ipToRootDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    // root -> set of observed hosts (www/subdomains/apex)
    var rootToHosts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    // root -> set of IPs encountered (for multi-IP-only report)
    var rootToIps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    var processed = 0;
    var skippedNoLink = 0;
    var skippedBadHost = 0;
    var skippedNoARecord = 0;

    foreach (var entry in entries)
    {
        processed++;

        var link = entry.Link?.Trim();
        if (string.IsNullOrWhiteSpace(link))
        {
            skippedNoLink++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] SKIP: empty Link (DirectoryEntryId={TryGetId(entry)})");
            continue;
        }

        if (!TryGetHostFromLink(link, out var originalHost))
        {
            skippedBadHost++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] SKIP: couldn't parse host from Link: {link}");
            continue;
        }

        // If host is already an IP, treat it as the hosting IP.
        if (IPAddress.TryParse(originalHost, out var parsedIp))
        {
            var ipStr = parsedIp.ToString();

            AddToMap(ipToRootDomains, ipStr, ipStr);
            AddToMap(rootToHosts, ipStr, originalHost);
            AddToMap(rootToIps, ipStr, ipStr);

            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] OK: host is already IP {ipStr} ({originalHost})");
            continue;
        }

        // .onion won't resolve via public DNS
        if (originalHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
        {
            skippedNoARecord++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] SKIP: .onion doesn't have public A-record: {originalHost}");
            continue;
        }

        var rootDomain = GetRegistrableDomainOrHost(originalHost, domainParser);

        // Track “connection” between subdomains and root
        AddToMap(rootToHosts, rootDomain, originalHost);

        // Prefer resolving the ROOT domain for consistency; fallback to original host; fallback to www.root
        var candidates = BuildResolutionCandidates(originalHost, rootDomain);

        Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] Resolving (root={rootDomain}) from: {string.Join(" -> ", candidates)}");

        string? hostUsed = null;
        List<string> ipv4s = new ();

        foreach (var candidate in candidates)
        {
            ipv4s = await GetOrResolveIPv4sAsync(candidate, dnsCache, dnsTimeout, maxAttemptsPerHost, retryDelay);

            if (ipv4s.Count > 0)
            {
                hostUsed = candidate;
                break;
            }
        }

        if (hostUsed == null || ipv4s.Count == 0)
        {
            skippedNoARecord++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] FAIL: no IPv4 A-record found for {originalHost} (root={rootDomain})");
            continue;
        }

        // Record all IPs for multi-IP detection
        foreach (var ip in ipv4s)
            AddToMap(rootToIps, rootDomain, ip);

        // Choose deterministic primary IP (prevents inflating counts when host returns many A's)
        var primaryIp = ipv4s.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();

        if (ipv4s.Count > 1)
            Console.WriteLine($"  Note: {hostUsed} returned multiple IPv4s: {string.Join(", ", ipv4s.OrderBy(x => x))} (primary={primaryIp})");

        AddToMap(ipToRootDomains, primaryIp, rootDomain);

        Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] OK: {originalHost} (resolved={hostUsed}) -> primary {primaryIp}");
    }

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("DONE");
    Console.WriteLine($"Processed:         {processed:n0}");
    Console.WriteLine($"Skipped (no Link): {skippedNoLink:n0}");
    Console.WriteLine($"Skipped (bad host):{skippedBadHost:n0}");
    Console.WriteLine($"Skipped (no A):    {skippedNoARecord:n0}");
    Console.WriteLine($"Unique IPs:        {ipToRootDomains.Count:n0}");
    Console.WriteLine($"Unique roots:      {rootToIps.Count:n0}");
    Console.WriteLine("============================================================");
    Console.WriteLine();

    // 1) MAIN REPORT: IPs by most root-domains first
    var orderedIps = ipToRootDomains
        .Select(kvp => new { Ip = kvp.Key, Roots = kvp.Value })
        .OrderByDescending(x => x.Roots.Count)
        .ThenBy(x => x.Ip, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine("=== IPs with most ROOT DOMAINS first (root-domain consolidated) ===");
    foreach (var row in orderedIps)
    {
        Console.WriteLine();
        Console.WriteLine($"{row.Ip} ({row.Roots.Count} root domains)");
        foreach (var root in row.Roots.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  - {root}");
    }

    // 2) CONNECTION REPORT: ONLY roots that have > 1 IP (single-IP roots omitted)
    Console.WriteLine();
    Console.WriteLine("=== ROOT DOMAINS that resolve to MULTIPLE IPs (connections; single-IP roots omitted) ===");

    var multiIpRoots = rootToIps
        .Select(kvp => new { Root = kvp.Key, Ips = kvp.Value })
        .Where(x => x.Ips.Count > 1) // ✅ ignore ones with only 1 IP associated
        .OrderByDescending(x => x.Ips.Count)
        .ThenBy(x => x.Root, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (multiIpRoots.Count == 0)
    {
        Console.WriteLine("None found.");
    }
    else
    {
        foreach (var row in multiIpRoots)
        {
            Console.WriteLine();
            Console.WriteLine($"{row.Root} -> {string.Join(", ", row.Ips.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");

            if (rootToHosts.TryGetValue(row.Root, out var hosts) && hosts.Count > 0)
            {
                Console.WriteLine("  Hosts seen:");
                foreach (var h in hosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"    - {h}");
            }
        }
    }

    // -------------------------
    // Local helpers for choice 3
    // -------------------------
    static List<string> BuildResolutionCandidates(string originalHost, string rootDomain)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(rootDomain) &&
            !rootDomain.Equals(originalHost, StringComparison.OrdinalIgnoreCase))
        {
            list.Add(rootDomain);
        }

        list.Add(originalHost);

        if (!string.IsNullOrWhiteSpace(rootDomain))
        {
            var wwwRoot = "www." + rootDomain;
            if (!wwwRoot.Equals(originalHost, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(wwwRoot);
            }
        }

        // De-dupe while preserving order
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<string>();
        foreach (var x in list)
        {
            var v = (x ?? "").Trim().Trim('.');
            if (string.IsNullOrWhiteSpace(v))
            {
                continue;
            }

            if (seen.Add(v))
            {
                deduped.Add(v);
            }
        }

        return deduped;
    }

    static async Task<List<string>> GetOrResolveIPv4sAsync(
        string host,
        Dictionary<string, List<string>> cache,
        TimeSpan timeout,
        int maxAttempts,
        TimeSpan retryDelay)
    {
        if (cache.TryGetValue(host, out var cached))
        {
            return cached;
        }

        var resolved = await ResolveIPv4sWithRetryAsync(host, timeout, maxAttempts, retryDelay);
        cache[host] = resolved;
        return resolved;
    }
}
else
{
    Console.WriteLine("Invalid choice. Exiting.");
}

static void AddToMap(Dictionary<string, HashSet<string>> map, string key, string value)
{
    if (!map.TryGetValue(key, out var set))
    {
        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        map[key] = set;
    }

    if (!string.IsNullOrWhiteSpace(value))
    {
        set.Add(value);
    }
}

static bool TryGetHostFromLink(string link, out string host)
{
    host = string.Empty;

    if (!link.Contains("://", StringComparison.OrdinalIgnoreCase))
    {
        link = "https://" + link;
    }

    if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
    {
        return false;
    }

    host = (uri.Host ?? string.Empty).Trim().Trim('.');
    return !string.IsNullOrWhiteSpace(host);
}

static async Task<List<string>> ResolveIPv4sWithRetryAsync(string host, TimeSpan timeout, int maxAttempts, TimeSpan retryDelay)
{
    host = (host ?? "").Trim().Trim('.');
    if (string.IsNullOrWhiteSpace(host))
    {
        return new List<string>();
    }

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var dnsTask = Dns.GetHostAddressesAsync(host);
            var completed = await Task.WhenAny(dnsTask, Task.Delay(timeout));

            if (completed != dnsTask)
            {
                throw new TimeoutException($"DNS lookup timed out after {timeout.TotalSeconds:n0}s");
            }

            var addresses = await dnsTask;

            return addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Attempt {attempt}/{maxAttempts} failed for {host}: {ex.Message}");
            if (attempt < maxAttempts)
            {
                await Task.Delay(retryDelay);
            }
        }
    }

    return new List<string>();
}

static string TryGetId(DirectoryEntry entry)
{
    var prop = entry.GetType().GetProperty("DirectoryEntryId");
    var val = prop?.GetValue(entry);
    return val?.ToString() ?? "?";
}

static bool IsRemovedEntry(DirectoryEntry entry)
{
    return entry.DirectoryStatus == DirectoryManager.Data.Enums.DirectoryStatus.Removed;
}

static string GetRegistrableDomainOrHost(string host, DomainParser domainParser)
{
    host = (host ?? "").Trim().Trim('.');
    if (string.IsNullOrWhiteSpace(host))
    {
        return host;
    }

    // Normalize IDN -> punycode for stability
    try { host = new IdnMapping().GetAscii(host); } catch { /* ignore */ }

    host = host.ToLowerInvariant();

    // IP stays IP
    if (IPAddress.TryParse(host, out _))
    {
        return host;
    }

    // strip www for consistency
    if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
    {
        host = host.Substring(4);
    }

    try
    {
        var info = domainParser.Parse(host);

        // RegistrableDomain is the "root" you want: example.com / example.co.uk / etc
        if (!string.IsNullOrWhiteSpace(info.RegistrableDomain))
        {
            return info.RegistrableDomain;
        }

        return host;
    }
    catch
    {
        // If PSL parse fails (localhost, weird hosts), just return cleaned host
        return host;
    }
}
