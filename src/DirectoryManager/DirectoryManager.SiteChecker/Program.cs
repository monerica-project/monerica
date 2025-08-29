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

// Function to create a new submission only if it doesn't already exist
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

    // Create a new submission with the appropriate details
    var submission = new Submission
    {
        Note = newNote,
        DirectoryStatus = DirectoryStatus.Removed,
        DirectoryEntryId = entry.DirectoryEntryId,
        Name = entry.Name,
        Link = entry.Link,
        SubmissionStatus = SubmissionStatus.Pending,
        SubCategoryId = entry.SubCategoryId,
        Description = entry.Description,
        Contact = entry.Contact,
        Link2 = entry.Link2,
        Link3 = entry.Link3,
        Location = entry.Location,
        Processor = entry.Processor,
        SubCategory = entry.SubCategory,
        CountryCode = entry.CountryCode,
        NoteToAdmin = "(automated submission)",
        PgpKey = entry.PgpKey,
        ProofLink = entry.ProofLink,
        Tags = entry.Tags,
    };

    await submissionRepository.CreateAsync(submission);
    Console.WriteLine($"Created submission for entry ID {entry.DirectoryEntryId} marked as '{SiteOfflineMessage}'.");
}

async Task CheckAndSubmitAsync(
    (int DirectoryEntryId, string Link) entry,
    IServiceProvider rootProvider)
{
    using var scope = rootProvider.CreateScope();

    // each scope gets its own DbContext
    var scopedEntriesRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryRepository>();
    var scopedSubmissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
    var checker = scope.ServiceProvider.GetRequiredService<WebPageChecker>();

    var uri = new Uri(entry.Link);
    bool isOnline;
    try
    {
        isOnline = await checker.IsOnlineAsync(uri);
    }
    catch
    {
        isOnline = false;
    }

    Console.WriteLine($"{entry.Link} is {(isOnline ? "online" : SiteOfflineMessage)}");

    if (!isOnline)
    {
        // load the entry *from this scope’s* repo
        var dirEntry = await scopedEntriesRepo.GetByIdAsync(entry.DirectoryEntryId);
        await CreateOfflineSubmissionIfNotExists(dirEntry, scopedSubmissionRepo);
    }
}