using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.SubmissionReviewer.Fetching;
using DirectoryManager.SubmissionReviewer.Review;
using DirectoryManager.TermsWatcher.Watch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Monerica terms watcher. Runs once a month (systemd timer): re-reads every live listing's own
// policy pages and compares the wording with last month's. Free when nothing changed; the AI is
// asked only about changed wording, within TermsWatcher:MonthlyAiCap. It never edits a listing:
// findings wait on /admin/terms-watch. The Anthropic key comes from the environment
// (Anthropic__ApiKey, root-only EnvironmentFile); without a key it still records changes.
Console.WriteLine($"TermsWatcher starting {DateTime.UtcNow:u}");

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var options = config.GetSection("TermsWatcher").Get<TermsWatchOptions>() ?? new TermsWatchOptions();
options.ApiKey = config["Anthropic:ApiKey"] ?? string.Empty;
options.BaseUrl = config["Anthropic:BaseUrl"] ?? options.BaseUrl;
options.TorHost = config["TorProxy:Host"] ?? options.TorHost;
options.TorPort = int.TryParse(config["TorProxy:Port"], out var tp) ? tp : options.TorPort;

// The page fetcher is the submission reviewer's (plain GET, 10 s timeout, 2 MB cap, Tor for
// onions, refuses private addresses); it only needs a user agent and the Tor proxy address.
var fetchOptions = new ReviewerOptions { UserAgent = options.UserAgent, TorHost = options.TorHost, TorPort = options.TorPort };

var services = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(o => DbProvider.Configure(o, config))
    .AddSingleton(options)
    .AddSingleton<IPageFetcher>(_ => new PageFetcher(fetchOptions))
    .AddSingleton<IClaudeTermsService>(_ => new ClaudeTermsService(new HttpClient(), options))
    .AddScoped<TermsWatchRunner>()
    .BuildServiceProvider();

using var scope = services.CreateScope();
using var cts = new CancellationTokenSource(TimeSpan.FromHours(5.5));
var t = await scope.ServiceProvider.GetRequiredService<TermsWatchRunner>().RunAsync(cts.Token);
Console.WriteLine($"TermsWatcher finished: {t.Checked} checked, {t.Baselines} first-time, {t.Unchanged} unchanged, {t.Changed} changed ({t.Flagged} flagged, {t.NotReviewed} not reviewed), {t.Unreadable} unreadable, {t.AiCalls} AI call(s).");
return 0;
