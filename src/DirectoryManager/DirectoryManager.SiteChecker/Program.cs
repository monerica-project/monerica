﻿// See https://aka.ms/new-console-template for more information
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.SiteChecker.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

// Get the User-Agent header value from the configuration
var userAgentHeader = config["UserAgent:Header"] ?? throw new InvalidOperationException("UserAgent header is missing.");

// Register services in the service container
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString("DefaultConnection")))
    .AddTransient<IApplicationDbContext, ApplicationDbContext>()
    .AddTransient<IDirectoryEntryRepository, DirectoryEntryRepository>()
    .AddTransient<ISubmissionRepository, SubmissionRepository>()
    .AddTransient<IDbInitializer, DbInitializer>()
    .AddTransient<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>()
    .AddTransient<ICategoryRepository, CategoryRepository>()
    .AddTransient<IExcludeUserAgentRepository, ExcludeUserAgentRepository>()
    .AddSingleton(new WebPageChecker(userAgentHeader))
    .BuildServiceProvider();

// Retrieve required services
var entriesRepo = serviceProvider.GetService<IDirectoryEntryRepository>()
    ?? throw new InvalidOperationException("The IDirectoryEntryRepository service is not registered.");
var submissionRepo = serviceProvider.GetService<ISubmissionRepository>()
    ?? throw new InvalidOperationException("The ISubmissionRepository service is not registered.");
var webPageChecker = serviceProvider.GetRequiredService<WebPageChecker>();

// Get all entries
var allEntries = await entriesRepo.GetAllAsync();
var offlines = new List<string>();

// Run the checks in parallel using Task.WhenAll for all entries
var tasks = allEntries
    .Where(entry => entry.DirectoryStatus != DirectoryStatus.Unknown &&
                    entry.DirectoryStatus != DirectoryStatus.Removed &&
                    !entry.Link.Contains(".onion"))
    .Select(async entry =>
    {
        var isOnline = await webPageChecker.IsWebPageOnlineAsync(new Uri(entry.Link));

        if (!isOnline)
        {
            isOnline = webPageChecker.IsWebPageOnlinePing(new Uri(entry.Link));
        }

        Console.WriteLine($"{entry.Link} is {(isOnline ? "online" : "offline")}");

        if (!isOnline)
        {
            offlines.Add($"{entry.Link}   -   ID: {entry.DirectoryEntryId}");
            await CreateOfflineSubmissionIfNotExists(entry, submissionRepo);
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

    if (existingSubmission != null && existingSubmission.Note?.Contains("site offline") == true)
    {
        Console.WriteLine($"Skipping submission for entry ID {entry.DirectoryEntryId}, already marked as offline.");
        return;
    }

    // Prepare the note, only appending " | site offline" if the current note isn't empty
    var newNote = string.IsNullOrWhiteSpace(entry.Note)
        ? "site offline"
        : $"{entry.Note} | site offline";

    // Create a new submission with the appropriate details
    var submission = new Submission
    {
        DirectoryEntryId = entry.DirectoryEntryId,
        Name = entry.Name,
        Link = entry.Link,
        DirectoryStatus = DirectoryStatus.Removed,
        Note = newNote,
        SubmissionStatus = SubmissionStatus.Pending,
        SubCategoryId = entry.SubCategoryId
    };

    await submissionRepository.CreateAsync(submission);
    Console.WriteLine($"Created submission for entry ID {entry.DirectoryEntryId} marked as offline.");
}
