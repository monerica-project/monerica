using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.ReviewOrderChecker.Check;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Monerica review order checker. Runs once per invocation (a systemd timer starts it every 10
// minutes): for reviews waiting for moderation that carry an order link, it opens the link in a
// headless browser, asks Claude what the page shows and stores the finding on the review.
// ADVISORY ONLY: it never approves, rejects, edits or tags a review. The Anthropic key comes from
// the environment (Anthropic__ApiKey, set by the unit's root-only EnvironmentFile).
Console.WriteLine($"ReviewOrderChecker starting {DateTime.UtcNow:u}");

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var options = config.GetSection("ReviewOrderChecker").Get<OrderCheckOptions>() ?? new OrderCheckOptions();
options.ApiKey = config["Anthropic:ApiKey"] ?? string.Empty;
options.BaseUrl = config["Anthropic:BaseUrl"] ?? options.BaseUrl;
options.TorHost = config["TorProxy:Host"] ?? options.TorHost;
options.TorPort = int.TryParse(config["TorProxy:Port"], out var tp) ? tp : options.TorPort;
if (string.IsNullOrWhiteSpace(options.RenderScriptPath))
{
    options.RenderScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Render", "render.py");
}

if (string.IsNullOrWhiteSpace(options.ApiKey))
{
    Console.Error.WriteLine("No Anthropic API key (Anthropic__ApiKey). Nothing checked.");
    return 1;
}

var services = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(o => DbProvider.Configure(o, config))
    .AddSingleton(options)
    .AddSingleton<IOrderPageReader, OrderPageReader>()
    .AddSingleton<IClaudeOrderCheckService>(_ => new ClaudeOrderCheckService(new HttpClient(), options))
    .AddSingleton<IPriceLookup>(_ => new PriceLookup(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }, options))
    .AddScoped<OrderCheckRunner>()
    .BuildServiceProvider();

using var scope = services.CreateScope();
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(9));
var done = await scope.ServiceProvider.GetRequiredService<OrderCheckRunner>().RunAsync(cts.Token);
Console.WriteLine($"ReviewOrderChecker finished: {done} review(s) checked.");
return 0;
