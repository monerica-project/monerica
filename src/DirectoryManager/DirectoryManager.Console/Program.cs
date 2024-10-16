// See https://aka.ms/new-console-template for more information
using System.Text;
using DirectoryManager.Console.Helpers;
using DirectoryManager.Console.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Set the base path to your project's directory
    .AddJsonFile("appsettings.json")
    .Build();

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        config.GetConnectionString("DefaultConnection")))
    .AddTransient<IApplicationDbContext, ApplicationDbContext>()
    .AddTransient<IDbInitializer, DbInitializer>()
    .AddTransient<IDirectoryEntryRepository, DirectoryEntryRepository>()
    .AddTransient<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>()
    .AddTransient<ICategoryRepository, CategoryRepository>()
    .AddTransient<IExcludeUserAgentRepository, ExcludeUserAgentRepository>()
    .BuildServiceProvider();

Console.WriteLine("Type 1 for page checker, 2 for user agent loaded");
var choice = Console.ReadLine();

if (choice == "1")
{
    var entries = serviceProvider.GetService<IDirectoryEntryRepository>() ??
        throw new InvalidOperationException("The IDirectoryEntryRepository service is not registered.");

    var allEntries = await entries.GetAllAsync();

    var offlines = new List<string>();

    // Run the checks in parallel using Task.WhenAll for all entries
    var tasks = allEntries
        .Where(entry => entry.DirectoryStatus != DirectoryManager.Data.Enums.DirectoryStatus.Unknown &&
                        entry.DirectoryStatus != DirectoryManager.Data.Enums.DirectoryStatus.Removed &&
                        !entry.Link.Contains(".onion"))
        .Select(async entry =>
        {
            var isOnline = await WebPageChecker.IsWebPageOnlineAsync(new Uri(entry.Link));

            if (!isOnline)
            {
                isOnline = WebPageChecker.IsWebPageOnlinePing(new Uri(entry.Link));
            }

            Console.WriteLine($"{entry.Link} is {(isOnline ? "online" : "offline")}");

            if (!isOnline)
            {
                offlines.Add($"{entry.Link}   -   ID: {entry.DirectoryEntryId}");
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

    Console.WriteLine("-----------------");
    Console.WriteLine("Done.");

    Console.ReadLine();
}
else if (choice == "2")
{
    var fileDir = Directory.GetCurrentDirectory() + @"\Content";
    var json = File.ReadAllText(Path.Combine(fileDir, "crawler-user-agents.json"), Encoding.UTF8);
    var jsonData = JsonConvert.DeserializeObject<List<UserAgentModel>>(json);

    if (jsonData == null)
    {
        return;
    }

    var userAgentRepository = serviceProvider.GetService<IExcludeUserAgentRepository>() ??
        throw new InvalidOperationException("The IExcludeUserAgentRepository service is not registered.");

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