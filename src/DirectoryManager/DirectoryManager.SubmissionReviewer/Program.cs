using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Monerica submission reviewer. Runs once per invocation (a systemd timer starts it every
// 10 minutes, like the other DirectoryManager jobs): promote stale previews, then review
// Pending submissions. The Anthropic key comes from the environment (Anthropic__ApiKey,
// set by the unit's root-only EnvironmentFile), never from a committed appsettings file.
Console.WriteLine($"SubmissionReviewer starting {DateTime.UtcNow:u}");

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var options = config.GetSection("SubmissionReviewer").Get<ReviewerOptions>() ?? new ReviewerOptions();
options.ApiKey = config["Anthropic:ApiKey"] ?? string.Empty;
options.BaseUrl = config["Anthropic:BaseUrl"] ?? options.BaseUrl;
options.TorHost = config["TorProxy:Host"] ?? options.TorHost;
options.TorPort = int.TryParse(config["TorProxy:Port"], out var tp) ? tp : options.TorPort;

var services = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(o => DbProvider.Configure(o, config))
    .AddSingleton(options)
    .AddSingleton<IPageFetcher, PageFetcher>()
    .AddSingleton<SubmissionDossierBuilder>()
    .AddSingleton<IClaudeReviewService>(_ => new ClaudeReviewService(new HttpClient(), options))
    .AddScoped<SubmissionReviewRunner>()
    .BuildServiceProvider();

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(9));
using var scope = services.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<SubmissionReviewRunner>();

if (string.IsNullOrWhiteSpace(options.ApiKey))
{
    // Still promote stale previews and run the free pre-filter; AI reviews just won't happen.
    Console.WriteLine("Anthropic:ApiKey is not set: only stale-preview promotion runs.");
    await runner.PromoteStalePreviewsAsync(cts.Token);
    return 0;
}

var reviewed = await runner.RunAsync(cts.Token);
Console.WriteLine($"SubmissionReviewer done: {reviewed} AI review(s).");
return 0;
