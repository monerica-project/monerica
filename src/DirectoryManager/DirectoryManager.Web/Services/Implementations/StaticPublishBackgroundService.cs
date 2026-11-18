using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.StaticPublishing;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Site-wide changes that have no edit to hook into, recorded as "*" markers (the 5-minute
    /// sitemap-gated crawl publishes them; the marker labels the pages it changes as expected, not drift):
    /// - a deploy (new templates on every page), written right at start-up so it precedes the crawl;
    /// - a sponsorship starting or ending on its own (the sponsor ticker is on every page);
    /// and pruning the publish log to its retention window.
    /// The "New" label ending at midnight is covered by the scheduled 00:05 UTC crawl.
    /// </summary>
    public class StaticPublishBackgroundService : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PruneEvery = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory scopes;
        private readonly IOptionsMonitor<StaticPublishOptions> options;
        private readonly ILogger<StaticPublishBackgroundService> logger;

        public StaticPublishBackgroundService(
            IServiceScopeFactory scopes,
            IOptionsMonitor<StaticPublishOptions> options,
            ILogger<StaticPublishBackgroundService> logger)
        {
            this.scopes = scopes;
            this.options = options;
            this.logger = logger;
        }

        /// <summary>When the running build was written (whole seconds, UTC); MinValue if unknown.</summary>
        public static DateTime BuildTimeUtc()
        {
            try
            {
                var location = typeof(StaticPublishBackgroundService).Assembly.Location;
                if (string.IsNullOrEmpty(location) || !File.Exists(location))
                {
                    return DateTime.MinValue;
                }

                var t = File.GetLastWriteTimeUtc(location);
                return new DateTime(t.Ticks - (t.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        private async Task MarkDeployAsync(CancellationToken ct)
        {
            try
            {
                if (this.options.CurrentValue.Mode == StaticPublishMode.Off)
                {
                    return;
                }

                using var scope = this.scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IStaticPublishRepository>();
                var build = BuildTimeUtc();
                var lastCrawl = await repo.GetLastFullCrawlAsync(false, ct);
                if (build > DateTime.MinValue && (lastCrawl == null || lastCrawl.PublishedUtc < build))
                {
                    await scope.ServiceProvider.GetRequiredService<IStaticPublishService>()
                        .RequestFullCrawlAsync($"deploy (build {build:yyyy-MM-dd HH:mm:ss} UTC)", ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.logger.LogWarning(ex, "Static publish: could not record the deploy marker.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.MarkDeployAsync(stoppingToken);

            try
            {
                await Task.Delay(StartDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var lastSponsorCheck = DateTime.UtcNow - StartDelay;
            var lastPrune = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                try
                {
                    using var scope = this.scopes.CreateScope();
                    var publish = scope.ServiceProvider.GetRequiredService<IStaticPublishService>();
                    var repo = scope.ServiceProvider.GetRequiredService<IStaticPublishRepository>();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    if (this.options.CurrentValue.Mode != StaticPublishMode.Off)
                    {
                        var changed = await db.SponsoredListings.AsNoTracking()
                            .Where(s => (s.CampaignStartDate > lastSponsorCheck && s.CampaignStartDate <= now)
                                     || (s.CampaignEndDate > lastSponsorCheck && s.CampaignEndDate <= now))
                            .Select(s => new { s.SponsoredListingId, s.DirectoryEntryId, s.CampaignStartDate })
                            .ToListAsync(stoppingToken);
                        if (changed.Count > 0)
                        {
                            var what = changed[0].CampaignStartDate > lastSponsorCheck ? "started" : "ended";
                            await publish.RequestFullCrawlAsync(
                                $"sponsorship {what} (listing #{changed[0].DirectoryEntryId}{(changed.Count > 1 ? $" +{changed.Count - 1} more" : string.Empty)})",
                                stoppingToken);
                        }
                    }

                    lastSponsorCheck = now;

                    if (now - lastPrune >= PruneEvery)
                    {
                        var days = Math.Max(1, this.options.CurrentValue.LogRetentionDays);
                        var removed = await repo.PruneLogAsync(now.AddDays(-days), stoppingToken);
                        if (removed > 0)
                        {
                            this.logger.LogInformation("Static publish: pruned {Count} log rows older than {Days} days.", removed, days);
                        }

                        lastPrune = now;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Static publish background check failed; retrying next minute.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
