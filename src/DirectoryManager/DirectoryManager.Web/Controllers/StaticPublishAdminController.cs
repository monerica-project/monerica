using DirectoryManager.Data.Models.StaticPublishing;
using DirectoryManager.Data.Models.TransferModels;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using DirectoryManager.Web.Models.StaticPublishing;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Controllers
{
    /// <summary>Admin view of publish-on-change: what is waiting, what was published, what failed,
    /// what the safety-net full crawl had to fix, plus manual republish / retry / full crawl.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [Route("admin/publishing")]
    public class StaticPublishAdminController : BaseController
    {
        public static readonly string[] Results =
        {
            StaticPublishConstants.ResultPublished, StaticPublishConstants.ResultUnchanged, StaticPublishConstants.ResultFailed,
            StaticPublishConstants.ResultRemoved, StaticPublishConstants.ResultWouldPublish, StaticPublishConstants.ResultWouldRemove,
        };

        public static readonly string[] Sources =
        {
            StaticPublishConstants.SourceInstant, StaticPublishConstants.SourceManual, StaticPublishConstants.SourceFullCrawl,
        };

        private readonly IStaticPublishRepository repository;
        private readonly IOptionsMonitor<StaticPublishOptions> options;

        public StaticPublishAdminController(
            IMemoryCache cache,
            IStaticPublishRepository repository,
            IOptionsMonitor<StaticPublishOptions> options)
            : base(cache)
        {
            this.repository = repository;
            this.options = options;
        }

        /// <summary>Hosts a pasted URL may be on to count as "a page of this site".</summary>
        public static IEnumerable<string> AllowedHosts(StaticPublishOptions o, string? requestHost)
        {
            var hosts = new List<string>();
            if (Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var u))
            {
                hosts.Add(u.Host);
                hosts.Add("app." + u.Host);
                hosts.Add("www." + u.Host);
            }

            if (!string.IsNullOrWhiteSpace(requestHost))
            {
                hosts.Add(requestHost);
            }

            return hosts;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? path, string? result, string? source, int page = 1, CancellationToken ct = default)
        {
            var o = this.options.CurrentValue;
            var queue = await this.repository.ListQueueAsync(ct);

            string? normalizedPath = null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var p = path.Trim();
                var prefix = p.Length > 1 && p.EndsWith('*') ? p[..^1] : null;
                if (prefix != null && StaticPublishPathRules.IsValidPath(prefix.Length > 1 ? prefix.TrimEnd('/') : prefix, out _))
                {
                    normalizedPath = p; // prefix search, e.g. /site/*
                }
                else if (StaticPublishPathRules.TryNormalize(p, AllowedHosts(o, this.Request.Host.Host), out var np, out _))
                {
                    normalizedPath = np;
                }
                else if (p == StaticPublishConstants.FullCrawlPath)
                {
                    normalizedPath = p;
                }
            }

            var vm = new StaticPublishAdminViewModel
            {
                Mode = o.Mode,
                InternalEndpointEnabled = o.InternalEndpointEnabled,
                MaxAttempts = o.MaxAttempts,
                PublicBaseUrl = o.PublicBaseUrl.TrimEnd('/'),
                WorkerLastPollUtc = StaticPublishWorkerStatus.LastPollUtc,
                FullCrawlRequest = queue.FirstOrDefault(q => q.Path == StaticPublishConstants.FullCrawlPath),
                Waiting = queue.Where(q => q.Path != StaticPublishConstants.FullCrawlPath && q.Attempts < o.MaxAttempts).ToList(),
                Failing = queue.Where(q => q.Path != StaticPublishConstants.FullCrawlPath && q.Attempts > 0)
                    .OrderByDescending(q => q.Attempts).ThenBy(q => q.RequestedUtc).ToList(),
                LastFullCrawl = await this.repository.GetLastFullCrawlAsync(false, ct),
                LastScheduledCrawl = await this.repository.GetLastFullCrawlAsync(true, ct),
                LastInstantPublish = await this.repository.GetLastInstantPublishAsync(ct),
                FilterPath = normalizedPath ?? path,
                FilterResult = Results.Contains(result) ? result : null,
                FilterSource = Sources.Contains(source) ? source : null,
            };

            vm.Drift = await this.repository.ListDriftAsync(DateTime.UtcNow.AddHours(-24), 200, ct);
            vm.LastCrawlReportUtc = StaticPublishWorkerStatus.LastCrawlReportUtc;

            if (normalizedPath != null && !normalizedPath.EndsWith('*'))
            {
                vm.LastPublishForPath = await this.repository.GetLastPublishForPathAsync(normalizedPath, ct);
            }

            vm.Log = await this.repository.ListLogAsync(
                new StaticPublishLogFilter
                {
                    Path = string.IsNullOrWhiteSpace(path) ? null : (normalizedPath ?? "//no-match"),
                    Result = vm.FilterResult,
                    Source = vm.FilterSource,
                    Page = Math.Clamp(page, 1, 10000),
                    PageSize = 50,
                },
                ct);

            return this.View("Index", vm);
        }

        [HttpPost("republish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Republish(string? path, CancellationToken ct = default)
        {
            var o = this.options.CurrentValue;
            if (o.Mode == StaticPublishMode.Off)
            {
                this.TempData["PublishError"] = "Publish on change is Off (StaticPublish:Mode), so nothing would pick this page up. The next full crawl will refresh it.";
                return this.Redirect("/admin/publishing");
            }

            if (!StaticPublishPathRules.TryNormalize(path, AllowedHosts(o, this.Request.Host.Host), out var normalized, out var error))
            {
                this.TempData["PublishError"] = error;
                return this.Redirect("/admin/publishing");
            }

            await this.repository.EnqueueAsync(
                new[] { normalized },
                "manual: republish requested by " + (this.User.Identity?.Name ?? "admin"),
                StaticPublishConstants.SourceManual,
                DateTime.UtcNow,
                ct);
            this.TempData["PublishMessage"] = $"Queued {normalized}. It is usually live within about 30 seconds.";
            return this.Redirect("/admin/publishing?path=" + Uri.EscapeDataString(normalized));
        }

        [HttpPost("retry-failed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryFailed(CancellationToken ct = default)
        {
            var n = await this.repository.RetryFailedAsync(ct);
            this.TempData["PublishMessage"] = n == 0 ? "Nothing was failing." : $"{n} page(s) will be tried again.";
            return this.Redirect("/admin/publishing");
        }

        [HttpPost("full-crawl")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FullCrawl(CancellationToken ct = default)
        {
            if (this.options.CurrentValue.Mode == StaticPublishMode.Off)
            {
                this.TempData["PublishError"] = "Publish on change is Off, so the publisher is not polling for requests. The scheduled full crawl still runs.";
                return this.Redirect("/admin/publishing");
            }

            await this.repository.EnqueueAsync(
                new[] { StaticPublishConstants.FullCrawlPath },
                "manual: full crawl requested by " + (this.User.Identity?.Name ?? "admin"),
                StaticPublishConstants.SourceForced,
                DateTime.UtcNow,
                ct);
            this.TempData["PublishMessage"] = "Full crawl requested. It starts within a few seconds and takes about 3–4 minutes.";
            return this.Redirect("/admin/publishing");
        }
    }
}
