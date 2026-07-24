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

// An onion must come back unreachable this many CONSECUTIVE runs before it is flagged
// offline (a single flaky/slow Tor run is never enough). Tunable via appsettings
// ("SiteCheck:OnionFlagThreshold"); defaults to 3. Definitive "gone" responses
// (404/410/521) still flag immediately, regardless of this.
var onionFlagThreshold = int.TryParse(config["SiteCheck:OnionFlagThreshold"], out var oft) && oft > 0
    ? oft
    : 3;

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
        DirectoryManager.Data.DbContextInfo.DbProvider.Configure(options, config))
    .AddDbRepositories()
    .AddSingleton(diagLogger)
    .AddSingleton(new WebPageChecker(userAgentHeader, timeout: null, logger: diagLogger, secondOpinion: new CheckHostClient(diagLogger)))
    .AddSingleton(new TorWebPageChecker(userAgentHeader, torHost, torPort, timeout: TimeSpan.FromSeconds(30), logger: diagLogger))
    .BuildServiceProvider();

var entriesRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var allEntries = await entriesRepo.GetAllIdsAndUrlsAsync();

// Clearnet: 10 concurrent checks.
// Tor: 6 concurrent. Higher concurrency can cause the occasional circuit-exhaustion
// false negative, but that is now harmless — a false "inconclusive" only nudges a
// streak by one and needs OnionFlagThreshold consecutive runs to matter, so it can
// never flag a live onion on its own. The payoff is a much faster sweep.
var semaphore = new SemaphoreSlim(10);
var torSemaphore = new SemaphoreSlim(6);

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
        catch
        {
        }

        Console.WriteLine($"[clearnet] {url} → {(isOnline ? "online" : "offline")}");

        if (!isOnline)
        {
            return true; // offline
        }
    }

    return false; // all online
}

// Checks a single .onion URL — returns its tri-state outcome (Online / Inconclusive / Offline).
async Task<CheckOutcome> CheckOnionUrlAsync(string url, TorWebPageChecker torChecker)
{
    var outcome = CheckOutcome.Inconclusive;
    try
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            outcome = await torChecker.CheckAsync(uri);
        }
    }
    catch
    {
        outcome = CheckOutcome.Inconclusive;
    }

    Console.WriteLine($"[onion] {url} → {outcome}");

    return outcome;
}

// Wraps CheckOnionUrlAsync with the Tor-specific semaphore
async Task<CheckOutcome> CheckOnionWithThrottleAsync(string url, TorWebPageChecker torChecker, SemaphoreSlim torSem)
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
    var scopedStatusRepo = scope.ServiceProvider.GetRequiredService<ISiteCheckStatusRepository>();
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
    // If Tor is unavailable / there is no onion, treat as Online (nothing to fail).
    bool hasOnion =
        torAvailable &&
        !string.IsNullOrWhiteSpace(dirEntry.Link2) &&
        dirEntry.Link2.Contains(".onion", StringComparison.OrdinalIgnoreCase);

    Task<CheckOutcome> onionTask = hasOnion
        ? CheckOnionWithThrottleAsync(dirEntry.Link2!, torChecker, torSem)
        : Task.FromResult(CheckOutcome.Online);

    // ── 3. Run both concurrently ──────────────────────────────────────────
    await Task.WhenAll(clearnetTask, onionTask);

    bool clearnetOffline = clearnetTask.Result;      // true = definitive clearnet offline (robust verdict)
    CheckOutcome onionOutcome = onionTask.Result;

    // ── 4. Fold into cross-run streaks, then decide what to flag ───────────
    // A single bad run is NOT enough for an onion (Tor times out on live sites all the
    // time). It must fail OnionFlagThreshold consecutive runs before it flags. Clearnet
    // verdicts are already vetted by an external second opinion, so they still flag on
    // the first confirmed offline. A definitive Offline (404/410/521) flags immediately
    // in both cases. Flagging only ever queues a PENDING review submission — never an
    // auto-removal — so the moderator has the final say.
    var status = await scopedStatusRepo.GetByDirectoryEntryIdAsync(entry.DirectoryEntryId)
                 ?? new SiteCheckStatus { DirectoryEntryId = entry.DirectoryEntryId };

    bool clearnetShouldFlag = false;
    bool onionShouldFlag = false;

    if (clearnetUrls.Count > 0)
    {
        if (clearnetOffline)
        {
            status.ClearnetFailStreak++;
            clearnetShouldFlag = true;
        }
        else
        {
            status.ClearnetFailStreak = 0;
        }
    }

    if (hasOnion)
    {
        if (onionOutcome == CheckOutcome.Online)
        {
            status.OnionFailStreak = 0;
        }
        else
        {
            status.OnionFailStreak++;
            onionShouldFlag = onionOutcome == CheckOutcome.Offline
                              || status.OnionFailStreak >= onionFlagThreshold;

            if (onionOutcome == CheckOutcome.Inconclusive)
            {
                Console.WriteLine(
                    $"[onion] {dirEntry.Link2} inconclusive streak {status.OnionFailStreak}/{onionFlagThreshold}" +
                    (onionShouldFlag ? " → threshold reached, flagging OFFLINE" : " → not flagging yet"));
            }
        }
    }

    status.LastCheckedUtc = DateTime.UtcNow;
    await scopedStatusRepo.UpsertAsync(status);

    if (clearnetShouldFlag || onionShouldFlag)
    {
        await CreateOfflineSubmissionIfNotExists(
            dirEntry,
            scopedSubmissionRepo,
            scopedEntryTagRepo,
            scopedAdditionalLinkRepo,
            clearnetShouldFlag,
            onionShouldFlag);
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
        Location = entry.Location,
        Processor = entry.Processor,
        CountryCode = entry.CountryCode,
        PgpKey = entry.PgpKey,
        ProofLink = entry.ProofLink,
        VideoLink = entry.VideoLink,
        FoundedDate = entry.FoundedDate,
        // Newer entry fields — carry them forward too, or approving this auto-submission
        // would blank them out on the live entry.
        Email = entry.Email,
        Messenger = entry.Messenger,
        Social = entry.Social,
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