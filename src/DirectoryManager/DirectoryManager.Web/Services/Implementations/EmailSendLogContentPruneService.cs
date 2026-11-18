using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Emails;
using Microsoft.Extensions.Options;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Hourly: deletes stored email content (EmailSendLogContents) not used by a send for
    /// <see cref="EmailSendLogOptions.ContentRetentionDays"/> days (default 90). The EmailSendLog rows
    /// stay (their content link is cleared, so the detail page says the content is no longer stored).
    /// Same pattern as the publish-log pruning in <see cref="StaticPublishBackgroundService"/>.
    /// </summary>
    public class EmailSendLogContentPruneService : BackgroundService
    {
        private static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan PruneEvery = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory scopes;
        private readonly IOptionsMonitor<EmailSendLogOptions> options;
        private readonly ILogger<EmailSendLogContentPruneService> logger;

        public EmailSendLogContentPruneService(
            IServiceScopeFactory scopes,
            IOptionsMonitor<EmailSendLogOptions> options,
            ILogger<EmailSendLogContentPruneService> logger)
        {
            this.scopes = scopes;
            this.options = options;
            this.logger = logger;
        }

        public async Task<int> PruneOnceAsync(DateTime nowUtc, CancellationToken ct)
        {
            var days = this.options.CurrentValue.EffectiveRetentionDays;
            using var scope = this.scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEmailSendLogContentRepository>();
            var removed = await repo.PruneAsync(nowUtc.AddDays(-days), ct);
            if (removed > 0)
            {
                this.logger.LogInformation("Email send log: pruned {Count} stored message content rows unused for {Days} days.", removed, days);
            }

            return removed;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await this.PruneOnceAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Email send log content pruning failed; retrying next hour.");
                }

                try
                {
                    await Task.Delay(PruneEvery, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
