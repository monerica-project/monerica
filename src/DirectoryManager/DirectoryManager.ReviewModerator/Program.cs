using System.Net.Sockets;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.ReviewModerator.Abstractions;
using DirectoryManager.ReviewModerator.Fetching;
using DirectoryManager.ReviewModerator.Moderation;
using DirectoryManager.ReviewModerator.Parsers;
using DirectoryManager.ReviewModerator.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Starting ReviewModerator");

const string UserAgentHeader = "UserAgent:Header";
const string TorProxyHostKey = "TorProxy:Host";
const string TorProxyPortKey = "TorProxy:Port";
const string MaxAttemptsKey = "ReviewModerator:MaxAttempts";
const string MaxConcurrencyKey = "ReviewModerator:MaxConcurrency";

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(DirectoryManager.Common.Constants.StringConstants.AppSettingsFileName, optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .Build();

var userAgent = config[UserAgentHeader] ?? "Mozilla/5.0 (compatible; MonericaReviewModerator/1.0; +https://monerica.com)";
var torHost = config[TorProxyHostKey] ?? "127.0.0.1";
var torPort = int.TryParse(config[TorProxyPortKey], out var tp) ? tp : 9050;
var maxAttempts = int.TryParse(config[MaxAttemptsKey], out var ma) ? ma : 8;          // 8 × 15min ≈ 2h window
var maxConcurrency = int.TryParse(config[MaxConcurrencyKey], out var mc) ? mc : 5;

var torAvailable = IsTorAvailable(torHost, torPort);
Console.WriteLine($"Tor proxy {(torAvailable ? "available" : "unavailable")} at {torHost}:{torPort}");

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(StringConstants.DefaultConnection)))
    .AddDbRepositories()
    .AddSingleton<IOrderProofParser, ChangeeOrderProofParser>()
    .AddSingleton<IOrderProofParser, GhostSwapOrderProofParser>()
    .AddSingleton<IOrderProofParser, TrocadorOrderProofParser>()
    .AddSingleton<IOrderProofParser, StereoSwapOrderProofParser>()
    .AddSingleton<IOrderProofParser, BitcoinVnOrderProofParser>()
    .AddSingleton<IOrderProofParser, LetsExchangeOrderProofParser>()
    .AddSingleton<IOrderProofParser, QuickExOrderProofParser>()
    .AddSingleton<OrderProofParserRegistry>()
    .AddSingleton<IPriceLookupService, MoneroMarketCapPriceLookupService>()
    .AddSingleton<IOrderProofFetcher>(_ => new HttpOrderProofFetcher(userAgent, torHost, torPort, torAvailable))
    .BuildServiceProvider();

// Find candidate reviews: pending, carrying an order URL, on a subcategory that requires
// review verification. NOTE: this assumes DirectoryEntry.SubCategory.RequireReviewVerification
// is reachable from the review graph — adjust the navigation names if they differ in your model.
List<int> candidateIds;
using (var scope = serviceProvider.CreateScope())
{
    var reviewRepo = scope.ServiceProvider.GetRequiredService<IDirectoryEntryReviewRepository>();

    candidateIds = await reviewRepo.Query()
        .Where(r => r.ModerationStatus == ReviewModerationStatus.Pending
                    && r.OrderUrl != null
                    && r.OrderUrl != string.Empty
                    && r.DirectoryEntry.SubCategory!.RequireReviewVerification)
        .OrderBy(r => r.DirectoryEntryReviewId)
        .Select(r => r.DirectoryEntryReviewId)
        .ToListAsync();
}

Console.WriteLine($"Found {candidateIds.Count} pending order-proof review(s) to evaluate.");

var semaphore = new SemaphoreSlim(maxConcurrency);
var counts = new System.Collections.Concurrent.ConcurrentDictionary<AutoModerationResult, int>();

var tasks = candidateIds.Select(async id =>
{
    await semaphore.WaitAsync();
    try
    {
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var moderator = new ReviewAutoModerator(
            sp.GetRequiredService<IDirectoryEntryReviewRepository>(),
            sp.GetRequiredService<IReviewTagRepository>(),
            sp.GetRequiredService<IDirectoryEntryReviewTagRepository>(),
            sp.GetRequiredService<OrderProofParserRegistry>(),
            sp.GetRequiredService<IOrderProofFetcher>(),
            sp.GetRequiredService<IPriceLookupService>(),
            maxAttempts);

        try
        {
            var outcome = await moderator.ProcessAsync(id, CancellationToken.None);
            counts.AddOrUpdate(outcome, 1, (_, v) => v + 1);
            Console.WriteLine($"  review {id}: {outcome}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  review {id}: ERROR {ex.Message}");
        }
    }
    finally
    {
        semaphore.Release();
    }
});

await Task.WhenAll(tasks);

Console.WriteLine("-----------------");
Console.WriteLine($"Approved: {counts.GetValueOrDefault(AutoModerationResult.AutoApproved)}, " +
                  $"Rejected: {counts.GetValueOrDefault(AutoModerationResult.AutoRejected)}, " +
                  $"Flagged: {counts.GetValueOrDefault(AutoModerationResult.Flagged)}, " +
                  $"Retry/none: {counts.GetValueOrDefault(AutoModerationResult.None)}");
Console.WriteLine("Done.");

static bool IsTorAvailable(string host, int port)
{
    try
    {
        using var c = new TcpClient();
        return c.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2)) && c.Connected;
    }
    catch
    {
        return false;
    }
}
