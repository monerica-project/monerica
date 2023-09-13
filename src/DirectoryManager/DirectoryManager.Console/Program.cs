// See https://aka.ms/new-console-template for more information
using DirectoryManager.Console.Helpers;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    .BuildServiceProvider();


var entries = serviceProvider.GetService<IDirectoryEntryRepository>();
 var allEntries = await entries.GetAllAsync();

var offlines = new List<string>();

foreach (var entry in allEntries)
{
    if (entry.DirectoryStatus == DirectoryManager.Data.Enums.DirectoryStatus.Unknown ||
        entry.DirectoryStatus == DirectoryManager.Data.Enums.DirectoryStatus.Removed)
    {
        continue;
    }

    if (entry.Link.Contains(".onion"))
    {
        continue;
    }

    var isOnline = await WebPageChecker.IsWebPageOnlineAsync(new Uri(entry.Link));

    Console.WriteLine($"{entry.Link} is {(isOnline ? "online" : "offline")}");

    if (!isOnline)
    {
        offlines.Add(entry.Link + "    -    ID: " + entry.Id);
    }
}

Console.WriteLine("-----------------");
Console.WriteLine("Offline:");
foreach (var offline in offlines)
{
    Console.WriteLine(offline);
}

Console.ReadLine();