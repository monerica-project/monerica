using System.Text;
using DirectoryManager.Console.Models;
using DirectoryManager.Console.Services;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Chat;

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory) // Set the base path to your project's directory
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName)
    .Build();

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        config.GetConnectionString(StringConstants.DefaultConnection)))
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
var choice = Console.ReadLine();

if (choice == "1")
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

    var response = await chat.CompleteChatAsync(messages)
                             .ConfigureAwait(false);

    var tags = response.Value.Content[0].Text;
}
else
{
    Console.WriteLine("Invalid choice. Exiting.");
}