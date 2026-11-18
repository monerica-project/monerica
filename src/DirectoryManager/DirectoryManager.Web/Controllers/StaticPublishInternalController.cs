using System.Globalization;
using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.StaticPublishing;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>
    /// Machine endpoints for the static-site publisher (GitSiteSyncer pending mode). Reachable only on
    /// the loopback internal port with the shared secret (<see cref="InternalEndpointGuard"/>);
    /// anything else gets a plain 404, as if the route did not exist.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("internal/static-publish")]
    public class StaticPublishInternalController : Controller
    {
        private readonly IStaticPublishRepository repository;
        private readonly IOptionsMonitor<StaticPublishOptions> options;
        private readonly ILogger<StaticPublishInternalController> logger;

        public StaticPublishInternalController(
            IStaticPublishRepository repository,
            IOptionsMonitor<StaticPublishOptions> options,
            ILogger<StaticPublishInternalController> logger)
        {
            this.repository = repository;
            this.options = options;
            this.logger = logger;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> Pending(int limit = 50, CancellationToken ct = default)
        {
            if (!this.Allowed())
            {
                return this.NotFound();
            }

            var o = this.options.CurrentValue;
            var now = DateTime.UtcNow;
            StaticPublishWorkerStatus.MarkPolled(now);

            if (o.Mode == StaticPublishMode.Off)
            {
                return this.Json(new { servedUtc = Iso(now), dryRun = false, fullCrawlRequested = false, paths = Array.Empty<string>() });
            }

            var crawl = await this.repository.GetFullCrawlRequestAsync(ct);
            // Only a forced request (admin button, or a change the sitemap gate cannot see) starts a crawl
            // now; a plain "*" marker is covered by the next 5-minute gated crawl.
            var crawlDue = StaticPublishConstants.IsForcedCrawlRequest(crawl)
                && crawl!.Attempts < o.MaxAttempts
                && (crawl.LastAttemptUtc == null || crawl.LastAttemptUtc <= now - StaticPublishRepository.BackoffAfter(crawl.Attempts));

            var rows = await this.repository.GetPendingAsync(Math.Clamp(limit, 1, 200), o.MaxAttempts, now, ct);
            return this.Json(new
            {
                servedUtc = Iso(now),
                dryRun = o.Mode == StaticPublishMode.DryRun,
                fullCrawlRequested = crawlDue,
                fullCrawlReason = crawlDue ? crawl!.Reason : null,
                paths = rows.Select(r => r.Path).ToList(),
            });
        }

        [HttpPost("result")]
        public async Task<IActionResult> Result([FromBody] StaticPublishResultReport? report, CancellationToken ct = default)
        {
            if (!this.Allowed())
            {
                return this.NotFound();
            }

            if (report == null)
            {
                return this.BadRequest();
            }

            DateTime? served = DateTime.TryParse(report.ServedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var s)
                ? DateTime.SpecifyKind(s, DateTimeKind.Utc)
                : null;
            var now = DateTime.UtcNow;

            if (report.FullCrawl != null)
            {
                StaticPublishWorkerStatus.MarkCrawlReported(now);
                await this.repository.RecordFullCrawlAsync(report.FullCrawl, served, now, this.options.CurrentValue.MaxFullCrawlPageRows, ct);
                return this.Json(new { ok = true });
            }

            var summary = await this.repository.ApplyResultsAsync(report.Results ?? new List<DirectoryManager.Data.Models.TransferModels.StaticPublishResultRow>(), served, now, ct);
            return this.Json(new { ok = true, summary.Logged, summary.Removed, summary.Failed, summary.KeptForRepublish });
        }

        private static string Iso(DateTime utc) => utc.ToString("o", CultureInfo.InvariantCulture);

        private bool Allowed()
        {
            var ok = InternalEndpointGuard.IsAllowed(
                this.HttpContext.Connection.LocalPort,
                this.HttpContext.Connection.RemoteIpAddress,
                this.Request.Headers,
                this.options.CurrentValue,
                out var reason);
            if (!ok && this.HttpContext.Connection.LocalPort == this.options.CurrentValue.InternalPort)
            {
                // Only worth a log line when someone reached the internal port without the secret.
                this.logger.LogWarning("Static publish internal endpoint refused: {Reason}.", reason);
            }

            return ok;
        }
    }
}
