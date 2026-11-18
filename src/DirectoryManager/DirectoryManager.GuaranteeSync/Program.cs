using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.GuaranteeSync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Monerica guarantee sync. Runs once a week (systemd timer): reads OrangeFren's guarantee feed and
// keeps the matching "deposit guarantee" amounts on Monerica listings current. No AI, no cost.
Console.WriteLine($"GuaranteeSync starting {DateTime.UtcNow:u}");

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var options = config.GetSection("GuaranteeSync").Get<GuaranteeSyncOptions>() ?? new GuaranteeSyncOptions();

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 5_000_000 };
http.DefaultRequestHeaders.UserAgent.ParseAdd("MonericaGuaranteeSync/1.0 (+https://monerica.com)");

string json;
try
{
    json = await http.GetStringAsync(options.ApiUrl);
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine("Could not reach the feed: " + ex.Message + " Nothing was changed.");
    return 1;
}

var feed = OrangeFrenGuarantees.Parse(json, out var problem);
if (feed == null)
{
    Console.Error.WriteLine("Feed problem: " + problem + " Nothing was changed.");
    return 1;
}

var services = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(o => DbProvider.Configure(o, config))
    .BuildServiceProvider();
using var scope = services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

var result = await OrangeFrenGuarantees.ApplyAsync(db, feed, options, CancellationToken.None);
if (result.Problem != null)
{
    Console.Error.WriteLine("Feed problem: " + result.Problem);
    return 1;
}

foreach (var c in result.Changes)
{
    Console.WriteLine(c.Kind == "added"
        ? $"  added   {c.Listing}: {c.NewBtc} BTC"
        : $"  updated {c.Listing}: {c.OldBtc?.ToString() ?? "?"} -> {c.NewBtc} BTC");
}

if (result.Unmatched.Count > 0)
{
    Console.WriteLine("In the feed but no matching Monerica listing (add an alias in GuaranteeSync:Aliases if it is listed): " + string.Join(", ", result.Unmatched));
}

if (result.NotInFeed.Count > 0)
{
    Console.WriteLine("Listings with an OrangeFren guarantee that the feed does not mention (left as they are): " + string.Join(", ", result.NotInFeed));
}

Console.WriteLine($"GuaranteeSync finished: {feed.Count} service(s) in the feed, {result.Changes.Count} change(s).");
return 0;
