using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.SiteChecker.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting SiteChecker");

const string UserAgentHeader = "UserAgent:Header";
const string TorProxyHostKey = "TorProxy:Host";
const string TorProxyPortKey = "TorProxy:Port";
const string SiteOfflineMessage = "site offline";

// Keep this in sync with DirectoryManager.Web.Constants.IntegerConstants.MaxAdditionalLinks.
// The edit form and the admin approval/sync path both cap related links at 3,
// so the offline submission must carry forward at most the same number.
const int MaxRelatedLinks = 3;

// Build configuration. appsettings.{Environment}.json (e.g. Production)
// auto-overlays appsettings.json — that's how deploy-jobs.sh injects the
// real DB connection and TorProxy settings on the server without touching
// the committed appsettings.json. Environment is set by the systemd unit
// (DOTNET_ENVIRONMENT=Production).
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName, optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .Build();

var userAgentHeader = config[UserAgentHeader]
    ?? throw new InvalidOperationException($"{UserAgentHeader} is missing.");

var torHost = config[TorProxyHostKey] ?? "127.0.0.1";
var torPort = int.TryParse(config[TorProxyPortKey], out var p) ? p : 9050;

// TryStartTorAsync's first check is IsTorAvailable(host, port) — a TCP probe.
// On Linux production, system tor@default is already listening on 9050, so
// this short-circuits and returns true without ever launching tor.exe (which
// is fine, because tor.exe wouldn't run on Linux anyway).
// On Windows dev, IsTorAvailable returns false, so it proceeds to start the
// bundled tor.exe from the publish output.
var torExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tor", "tor.exe");
bool torAvailable = await TorWebPageChecker.TryStartTorAsync(torExePath, torHost, torPort);

// ── Diagnostic logger ─────────────────────────────────────────────────────
var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
var logPath = Path.Combine(logDir, "sitecheck-diag.log");
var diagLogger = new DiagnosticLogger(logPath);
Console.WriteLine($"Diagnostic log: {logPath}");

// Register services
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(StringConstants.DefaultConnection)))
    .AddDbRepositories()
    .AddSingleton(diagLogger)
    .AddSingleton(new WebPageChecker(userAgentHeader, timeout: null, logger: diagLogger))
    .AddSingleton(new TorWebPageChecker(userAgentHeader, torHost, torPort, timeout: null, logger: diagLogger))
    .BuildServiceProvider();

var entriesRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var allEntries = await entriesRepo.GetAllIdsAndUrlsAsync();

// Clearnet: 10 concurrent checks
// Tor: max 2 concurrent — circuit exhaustion causes false negatives
var semaphore = new SemaphoreSlim(10);
var torSemaphore = new SemaphoreSlim(2);

var tasks = allEntries
    .Select(async entry =>
    {
        await semaphore.WaitAsync();
        try
        {
            await CheckAndSubmitAsync((entry.DirectoryEntryId, entry.Link), serviceProvider, torAvailable, torSemaphore);
        }
        finally
        {
            semaphore.Release();
        }
    });

await Task.WhenAll(tasks);

Console.WriteLine("-----------------");
Console.WriteLine("Done.");

diagLogger.Dispose();

// ── Helpers ───────────────────────────────────────────────────────────────

// Checks each clearnet URL in sequence — returns true if ANY are offline
async Task<bool> CheckClearnetUrlsAsync(List<string> urls, WebPageChecker checker)
{
    foreach (var url in urls)
    {
        bool isOnline = false;
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                isOnline = await checker.IsOnlineAsync(uri);
            }
        }
        catch { }

        Console.WriteLine($"[clearnet] {url} → {(isOnline ? "online" : "offline")}");

        if (!isOnline)
        {
            return true; // offline
        }
    }

    return false; // all online
}

// Checks a single .onion URL — returns true if offline
async Task<bool> CheckOnionUrlAsync(string url, TorWebPageChecker torChecker)
{
    bool isOnline = false;
    try
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            isOnline = await torChecker.IsOnlineAsync(uri);
        }
    }
    catch { }

    Console.WriteLine($"[onion] {url} → {(isOnline ? "online" : "offline")}");

    return !isOnline;
}

// Wraps CheckOnionUrlAsync with the Tor-specific semaphore
async Task<bool> CheckOnionWithThrottleAsync(string url, TorWebPageChecker torChecker, SemaphoreSlim torSem)
{
    await torSem.WaitAsync();
    try
    {
        return await CheckOnionUrlAsync(url, torChecker);
    }
    finally
    {
        torSem.Release();
    }
}

async Task CheckAndSubmitAsync(
    (int DirectoryEntryId, string Link) entry,
    IServiceProvider rootProvider,
    bool torAvailable,
    SemaphoreSlim torSem)
{
    using var scope = rootProvider.CreateScope();

    var scopedEntriesRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryRepository>();
    var scopedSubmissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
    var scopedEntryTagRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryTagRepository>();
    var scopedAdditionalLinkRepo = scope.ServiceProvider.GetRequiredService<IAdditionalLinkRepository>();
    var checker = scope.ServiceProvider.GetRequiredService<WebPageChecker>();
    var torChecker = scope.ServiceProvider.GetRequiredService<TorWebPageChecker>();

    var dirEntry = await scopedEntriesRepo.GetByIdAsync(entry.DirectoryEntryId);
    if (dirEntry == null)
    {
        Console.WriteLine($"Entry {entry.DirectoryEntryId} not found.");
        return;
    }

    // ── 1. Build clearnet task (Link, LinkA) ──────────────────────────────
    var clearnetUrls = new[] { dirEntry.Link, dirEntry.LinkA }
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .Where(u => !u!.Contains(".onion", StringComparison.OrdinalIgnoreCase) &&
                    !u!.Contains(".i2p", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    Task<bool> clearnetTask = clearnetUrls.Count > 0
        ? CheckClearnetUrlsAsync(clearnetUrls, checker)
        : Task.FromResult(false);

    // ── 2. Build onion task (Link2) ───────────────────────────────────────
    // If Tor is unavailable, return false (skip — do NOT treat as offline)
    Task<bool> onionTask =
        torAvailable &&
        !string.IsNullOrWhiteSpace(dirEntry.Link2) &&
        dirEntry.Link2.Contains(".onion", StringComparison.OrdinalIgnoreCase)
            ? CheckOnionWithThrottleAsync(dirEntry.Link2, torChecker, torSem)
            : Task.FromResult(false); // skipped = not offline

    // ── 3. Run both concurrently ──────────────────────────────────────────
    var results = await Task.WhenAll(clearnetTask, onionTask);

    bool clearnetOffline = results[0];
    bool onionOffline = results[1];

    if (clearnetOffline || onionOffline)
    {
        await CreateOfflineSubmissionIfNotExists(
            dirEntry,
            scopedSubmissionRepo,
            scopedEntryTagRepo,
            scopedAdditionalLinkRepo,
            clearnetOffline,
            onionOffline);
    }
}

async Task CreateOfflineSubmissionIfNotExists(
    DirectoryEntry entry,
    ISubmissionRepository submissionRepository,
    IDirectoryEntryTagRepository entryTagRepository,
    IAdditionalLinkRepository additionalLinkRepository,
    bool clearnetOffline,
    bool onionOffline)
{
    var existingSubmission = await submissionRepository.GetByLinkAndStatusAsync(entry.Link, SubmissionStatus.Pending);

    if (existingSubmission != null && existingSubmission.Note?.Contains(SiteOfflineMessage) == true)
    {
        Console.WriteLine($"Skipping submission for entry ID {entry.DirectoryEntryId}, already marked as '{SiteOfflineMessage}'.");
        return;
    }

    // Build a specific offline reason based on which links are down
    var offlineReason = (clearnetOffline, onionOffline) switch
    {
        (true, true) => "site offline (clearnet and tor link offline)",
        (true, false) => "site offline (clearnet link offline)",
        (false, true) => "site offline (tor link offline)",
        _ => SiteOfflineMessage
    };

    var newNote = string.IsNullOrWhiteSpace(entry.Note)
        ? offlineReason
        : $"{entry.Note} | {offlineReason}";

    // ── Tags ──────────────────────────────────────────────────────────────
    // Tags are NOT stored on the DirectoryEntry row. They live in the
    // DirectoryEntryTag join table and must be pulled via the tag repo —
    // exactly like the edit flow (SubmissionController.SubmitEdit). The old
    // reflection-based reads of "SelectedTagIdsCsv"/"Tags" on DirectoryEntry
    // always returned null because those members don't exist there, which is
    // why offline submissions lost their tags.
    var entryTags = await entryTagRepository.GetTagsForEntryAsync(entry.DirectoryEntryId);

    var tagNames = string.Join(
        ", ",
        entryTags
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

    var selectedTagIdsCsv = string.Join(
        ",",
        entryTags.Select(t => t.TagId).Distinct());

    // ── Additional / related links ─────────────────────────────────────────
    // Additional links also live in their own table (AdditionalLink), read via
    // IAdditionalLinkRepository — same source the edit flow uses. Carry them
    // forward in the same order and with the same cap as the edit/approval path.
    var additional = await additionalLinkRepository.GetByDirectoryEntryIdAsync(entry.DirectoryEntryId);

    var relatedLinks = (additional ?? new List<AdditionalLink>())
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.AdditionalLinkId)
        .Select(x => x.Link)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(MaxRelatedLinks)
        .ToList();

    var submission = new Submission
    {
        SubmissionStatus = SubmissionStatus.Pending,
        DirectoryEntryId = entry.DirectoryEntryId,
        SubCategoryId = entry.SubCategoryId,
        DirectoryStatus = DirectoryStatus.Removed,
        Name = entry.Name,
        Link = entry.Link,
        Link2 = entry.Link2,
        Link3 = entry.Link3,
        Description = entry.Description,
        Contact = entry.Contact,
        Location = entry.Location,
        Processor = entry.Processor,
        CountryCode = entry.CountryCode,
        PgpKey = entry.PgpKey,
        ProofLink = entry.ProofLink,
        VideoLink = entry.VideoLink,
        FoundedDate = entry.FoundedDate,
        Note = newNote,
        NoteToAdmin = "(automated submission)",
        Tags = string.IsNullOrWhiteSpace(tagNames) ? null : tagNames,
        SelectedTagIdsCsv = entryTags.Count == 0 ? null : selectedTagIdsCsv,
        RelatedLinks = relatedLinks,
        SuggestedSubCategory = null,
        IpAddress = null
    };

    await submissionRepository.CreateAsync(submission);
    Console.WriteLine(
        $"Created submission for entry ID {entry.DirectoryEntryId}: '{offlineReason}' " +
        $"(tags: {entryTags.Count}, related links: {relatedLinks.Count}).");
}