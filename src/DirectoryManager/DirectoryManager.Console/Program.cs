using System.Net;
using System.Net.Sockets;
using System.Text;
using DirectoryManager.Console.Models;
using DirectoryManager.Console.Services;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models; // ✅ DirectoryEntry
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
Console.WriteLine("Type 3 to group sites by A-record IP (non-removed entries)");
var choice = Console.ReadLine();

if (choice == "1")
{
    var fileDir = Directory.GetCurrentDirectory() + @"\Content";
    var json = File.ReadAllText(Path.Combine(fileDir, "crawler-user-agents.json"), Encoding.UTF8);
    var jsonData = JsonConvert.DeserializeObject<List<UserAgentModel>>(json);

    if (jsonData == null)
        return;

    var userAgentRepository = serviceProvider.GetService<IExcludeUserAgentRepository>()
        ?? throw new InvalidOperationException("The IExcludeUserAgentRepository service is not registered.");

    foreach (var instance in jsonData)
    {
        if (instance.Instances == null)
            continue;

        foreach (var userAgent in instance.Instances)
        {
            if (userAgentRepository.Exists(userAgent))
                continue;

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

    var aiTagService = new AITagService(
        openAi,
        directoryEntryRepo,
        tagRepo,
        entryTagRepo);

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

    Console.WriteLine("Loading DirectoryEntries from DB...");
    var db = serviceProvider.GetRequiredService<ApplicationDbContext>();

    // Pull everything, then filter "removed" in-memory so this compiles regardless of your exact column names.
    var allEntries = await db.Set<DirectoryEntry>()
        .AsNoTracking()
        .ToListAsync();

    Console.WriteLine($"Loaded {allEntries.Count:n0} entries.");

    var entries = allEntries
        .Where(e => !IsRemovedEntry(e))
        .ToList();

    Console.WriteLine($"Non-removed entries: {entries.Count:n0}");
    Console.WriteLine();

    // ip -> set of sites (hostnames)
    var ipToSites = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

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

        if (!TryGetHostFromLink(link, out var host))
        {
            skippedBadHost++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] SKIP: couldn't parse host from Link: {link}");
            continue;
        }

        // If the host is already an IP, treat it as the hosting IP.
        if (IPAddress.TryParse(host, out var parsedIp))
        {
            var ipStr = parsedIp.ToString();
            AddSite(ipToSites, ipStr, host);
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] OK: host is already IP {ipStr} ({host})");
            continue;
        }

        // .onion and similar won't resolve via public DNS.
        if (host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
        {
            skippedNoARecord++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] SKIP: .onion doesn't have public A-record: {host}");
            continue;
        }

        Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] Resolving A-record for {host} ...");

        var ip = await ResolveIPv4WithRetryAsync(
            host,
            timeout: dnsTimeout,
            maxAttempts: maxAttemptsPerHost,
            retryDelay: retryDelay);

        if (ip == null)
        {
            skippedNoARecord++;
            Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] FAIL: no IPv4 A-record found for {host}");
            continue;
        }

        AddSite(ipToSites, ip, host);
        Console.WriteLine($"[{processed:n0}/{entries.Count:n0}] OK: {host} -> {ip}");
    }

    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine("DONE");
    Console.WriteLine($"Processed:         {processed:n0}");
    Console.WriteLine($"Skipped (no Link): {skippedNoLink:n0}");
    Console.WriteLine($"Skipped (bad host):{skippedBadHost:n0}");
    Console.WriteLine($"Skipped (no A):    {skippedNoARecord:n0}");
    Console.WriteLine($"Unique IPs:        {ipToSites.Count:n0}");
    Console.WriteLine("============================================================");
    Console.WriteLine();

    // Sort IPs by number of sites DESC, then IP ASC
    var ordered = ipToSites
        .Select(kvp => new { Ip = kvp.Key, Sites = kvp.Value })
        .OrderByDescending(x => x.Sites.Count)
        .ThenBy(x => x.Ip, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Console.WriteLine("=== IPs with most sites first ===");
    foreach (var row in ordered)
    {
        Console.WriteLine();
        Console.WriteLine($"{row.Ip} ({row.Sites.Count} sites)");
        foreach (var site in row.Sites.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  - {site}");
        }
    }
}
else
{
    Console.WriteLine("Invalid choice. Exiting.");
}

// =========================
// Helpers
// =========================

static void AddSite(Dictionary<string, HashSet<string>> ipToSites, string ip, string siteHost)
{
    if (!ipToSites.TryGetValue(ip, out var set))
    {
        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ipToSites[ip] = set;
    }

    set.Add(siteHost);
}

static bool TryGetHostFromLink(string link, out string host)
{
    host = string.Empty;

    // If it lacks a scheme, Uri.TryCreate treats it as relative; add https://
    if (!link.Contains("://", StringComparison.OrdinalIgnoreCase))
        link = "https://" + link;

    if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        return false;

    host = uri.Host?.Trim() ?? string.Empty;
    return !string.IsNullOrWhiteSpace(host);
}

static async Task<string?> ResolveIPv4WithRetryAsync(string host, TimeSpan timeout, int maxAttempts, TimeSpan retryDelay)
{
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var dnsTask = Dns.GetHostAddressesAsync(host);
            var completed = await Task.WhenAny(dnsTask, Task.Delay(timeout));

            if (completed != dnsTask)
                throw new TimeoutException($"DNS lookup timed out after {timeout.TotalSeconds:n0}s");

            var addresses = await dnsTask;

            // A-record = IPv4
            var ipv4s = addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ipv4s.Count == 0)
                return null;

            // If multiple, choose a stable "primary" (sorted first) so your report is deterministic.
            if (ipv4s.Count > 1)
                Console.WriteLine($"  Note: {host} returned multiple IPv4s: {string.Join(", ", ipv4s)} (using {ipv4s[0]})");

            return ipv4s[0];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Attempt {attempt}/{maxAttempts} failed for {host}: {ex.Message}");

            if (attempt < maxAttempts)
                await Task.Delay(retryDelay);
        }
    }

    return null;
}

static string TryGetId(DirectoryEntry entry)
{
    // Best-effort logging: if you have DirectoryEntryId, show it. Otherwise show "?"
    var prop = entry.GetType().GetProperty("DirectoryEntryId");
    var val = prop?.GetValue(entry);
    return val?.ToString() ?? "?";
}

static bool IsRemovedEntry(DirectoryEntry entry)
{
    return entry.DirectoryStatus == DirectoryManager.Data.Enums.DirectoryStatus.Removed;
}
