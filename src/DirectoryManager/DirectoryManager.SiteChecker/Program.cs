using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions; // Import for AddRepositories
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.SiteChecker.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting SiteChecker");

const string UserAgentHeader = "UserAgent:Header";
const string SiteOfflineMessage = "site offline";

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName)
    .Build();

// Get the User-Agent header value from the configuration
var userAgentHeader = config[UserAgentHeader] ?? throw new InvalidOperationException($"{UserAgentHeader} is missing.");

// Register services in the service container
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(StringConstants.DefaultConnection)))
    .AddDbRepositories() // Register all repositories through the centralized method
    .AddSingleton(new WebPageChecker(userAgentHeader)) // WebPageChecker
    .BuildServiceProvider();

// Retrieve required services
var entriesRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var webPageChecker = serviceProvider.GetRequiredService<WebPageChecker>();

// Get all entries
var allEntries = await entriesRepo.GetAllIdsAndUrlsAsync();
var offlines = new List<string>();

// Kick off one Task per entry
var tasks = allEntries
  .Where(e =>
    !e.Link.Contains(".onion", StringComparison.OrdinalIgnoreCase) &&
    !e.Link.Contains(".i2p", StringComparison.OrdinalIgnoreCase))
  .Select(entry => CheckAndSubmitAsync((entry.DirectoryEntryId, entry.Link), serviceProvider));

// Wait for all the tasks to complete
await Task.WhenAll(tasks);

Console.WriteLine("-----------------");

if (offlines.Count > 0)
{
    Console.WriteLine("Offline:");
    foreach (var offline in offlines)
    {
        Console.WriteLine(offline);
    }
}
else
{
    Console.WriteLine("None offline");
}

async Task CreateOfflineSubmissionIfNotExists(
    DirectoryEntry entry,
    ISubmissionRepository submissionRepository)
{
    // Check if there's already a pending "offline" submission for this entry
    var existingSubmission = await submissionRepository.GetByLinkAndStatusAsync(entry.Link, SubmissionStatus.Pending);

    if (existingSubmission != null && existingSubmission.Note?.Contains(SiteOfflineMessage) == true)
    {
        Console.WriteLine($"Skipping submission for entry ID {entry.DirectoryEntryId}, already marked as '{SiteOfflineMessage}'.");
        return;
    }

    // Prepare the note, only appending " | site offline" if the current note isn't empty
    var newNote = string.IsNullOrWhiteSpace(entry.Note)
        ? SiteOfflineMessage
        : $"{entry.Note} | {SiteOfflineMessage}";

    // ✅ Carry forward "real tag data" if your DirectoryEntry has it.
    // You currently set Tags = entry.Tags, but in your Submission flow the checkbox tags live in SelectedTagIdsCsv.
    // Only set these if your DirectoryEntry actually has them.
    string? selectedTagIdsCsv = null;
    try
    {
        // If your DirectoryEntry has SelectedTagIdsCsv (common in your newer flow), copy it.
        // If it doesn't exist on DirectoryEntry, just leave null.
        var prop = entry.GetType().GetProperty("SelectedTagIdsCsv");
        if (prop != null)
        {
            selectedTagIdsCsv = prop.GetValue(entry) as string;
        }
    }
    catch
    {
        selectedTagIdsCsv = null;
    }

    // ✅ Related links: your Submission stores these in RelatedLinksJson (via the RelatedLinks property).
    // If DirectoryEntry has related links (either json or a list), copy them.
    List<string> relatedLinks = new List<string>();
    try
    {
        // If your DirectoryEntry has RelatedLinks (List<string>) copy it
        var relatedProp = entry.GetType().GetProperty("RelatedLinks");
        if (relatedProp != null)
        {
            if (relatedProp.GetValue(entry) is IEnumerable<string> rel)
            {
                relatedLinks = rel
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        else
        {
            // Or if it has RelatedLinksJson, copy/deserialize it
            var jsonProp = entry.GetType().GetProperty("RelatedLinksJson");
            if (jsonProp != null)
            {
                var json = jsonProp.GetValue(entry) as string;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    relatedLinks = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)
                        ?? new List<string>();
                }
            }
        }
    }
    catch
    {
        relatedLinks = new List<string>();
    }

    // Create a new submission with the appropriate details
    var submission = new Submission
    {
        // core
        SubmissionStatus = SubmissionStatus.Pending,
        DirectoryEntryId = entry.DirectoryEntryId,
        SubCategoryId = entry.SubCategoryId,
        DirectoryStatus = DirectoryStatus.Removed,

        // content copy
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

        // notes
        Note = newNote,
        NoteToAdmin = "(automated submission)",

        // tags
        Tags = entry.Tags,                 // typed tags (legacy / display)
        SelectedTagIdsCsv = selectedTagIdsCsv, // ✅ checkbox tags (modern flow)

        // related links (writes RelatedLinksJson via setter)
        RelatedLinks = relatedLinks,

        // keep these empty for automation
        SuggestedSubCategory = null,
        IpAddress = null
    };

    // ❌ Do NOT set SubCategory nav property here.
    // submission.SubCategory = entry.SubCategory;  // <-- remove

    await submissionRepository.CreateAsync(submission);
    Console.WriteLine($"Created submission for entry ID {entry.DirectoryEntryId} marked as '{SiteOfflineMessage}'.");
}

async Task CheckAndSubmitAsync(
    (int DirectoryEntryId, string Link) entry,
    IServiceProvider rootProvider)
{
    using var scope = rootProvider.CreateScope();

    var scopedEntriesRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryRepository>();
    var scopedSubmissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
    var checker = scope.ServiceProvider.GetRequiredService<WebPageChecker>();

    // Load full entry so we can read Link + LinkA
    var dirEntry = await scopedEntriesRepo.GetByIdAsync(entry.DirectoryEntryId);
    if (dirEntry == null)
    {
        Console.WriteLine($"Entry {entry.DirectoryEntryId} not found.");
        return;
    }

    // Build list of URLs to check (Link + LinkA if present), skip onion/i2p/empty, dedupe
    var urlsToCheck = new[] { dirEntry.Link, dirEntry.LinkA }
        .Where(u => !string.IsNullOrWhiteSpace(u))
        .Where(u => !u.Contains(".onion", StringComparison.OrdinalIgnoreCase) &&
                    !u.Contains(".i2p", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (urlsToCheck.Count == 0)
    {
        Console.WriteLine($"Entry {dirEntry.DirectoryEntryId} has only onion/i2p or empty URLs. Skipping.");
        return;
    }

    // Offline if ANY eligible URL is offline (404/timeout/invalid/etc.)
    bool anyOffline = false;

    foreach (var url in urlsToCheck)
    {
        bool isOnline = false;

        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                isOnline = await checker.IsOnlineAsync(uri);
            }
            else
            {
                // Invalid URL: treat as offline
                isOnline = false;
            }
        }
        catch
        {
            isOnline = false;
        }

        Console.WriteLine($"{url} is {(isOnline ? "online" : SiteOfflineMessage)}");

        if (!isOnline)
        {
            anyOffline = true;
            break; // short-circuit on first offline URL
        }
    }

    if (anyOffline)
    {
        await CreateOfflineSubmissionIfNotExists(dirEntry, scopedSubmissionRepo);
    }
}