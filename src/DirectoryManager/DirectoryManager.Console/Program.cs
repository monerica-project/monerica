﻿// See https://aka.ms/new-console-template for more information
using DirectoryManager.Console.Helpers;
using DirectoryManager.Console.Models;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Text;

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