// See https://aka.ms/new-console-template for more information
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
    .AddRepositories() // Register all repositories through the centralized method
    .AddSingleton(new WebPageChecker(userAgentHeader)) // WebPageChecker
    .BuildServiceProvider();

// Retrieve required services
var entriesRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var webPageChecker = serviceProvider.GetRequiredService<WebPageChecker>();

// Get all entries
var allEntries = await entriesRepo.GetAllAsync();
var offlines = new List<string>();

// Run the checks in parallel using Task.WhenAll for all entries
var tasks = allEntries
    .Where(entry =>
        // only include entries whose status is neither Unknown nor Removed
        entry.DirectoryStatus != DirectoryStatus.Unknown &&
        entry.DirectoryStatus != DirectoryStatus.Removed &&

        // and whose link does NOT contain an .onion or .i2p address
        !entry.Link.Contains(".onion", StringComparison.OrdinalIgnoreCase) &&
        !entry.Link.Contains(".i2p", StringComparison.OrdinalIgnoreCase))
    .Select(async entry =>
    {
        // Create a new scope for each task to isolate DbContext instances
        using var scope = serviceProvider.CreateScope();
        var scopedSubmissionRepo = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();

        // check online status
        var uri = new Uri(entry.Link);
        var isOnline = await webPageChecker.IsWebPageOnlineAsync(uri)
                       || webPageChecker.IsWebPageOnlinePing(uri);

        Console.WriteLine($"{entry.Link} is {(isOnline ? "online" : SiteOfflineMessage)}");

        if (!isOnline)
        {
            offlines.Add($"{entry.Link} - ID: {entry.DirectoryEntryId}");
            await CreateOfflineSubmissionIfNotExists(entry, scopedSubmissionRepo);
        }
    });

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
    };

    await submissionRepository.CreateAsync(submission);
    Console.WriteLine($"Created submission for entry ID {entry.DirectoryEntryId} marked as '{SiteOfflineMessage}'.");
}