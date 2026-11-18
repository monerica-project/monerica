using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Every few minutes, asks BTCPay about recent sponsorship invoices that are not paid on our
    /// side and accepts the ones that were paid after their timer ran out (or whose "settled"
    /// webhook never arrived). The webhook does the same check as soon as a late payment is seen;
    /// this pass catches the moment the payment confirms and anything the webhook missed.
    /// </summary>
    public class SponsorLatePaymentSweeper : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        // Timer (15 min) + grace (60 min) + time to confirm, with room to spare.
        public static readonly TimeSpan Window = TimeSpan.FromHours(6);
        private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(75);

        private readonly IServiceScopeFactory scopes;
        private readonly ILogger<SponsorLatePaymentSweeper> logger;

        public SponsorLatePaymentSweeper(IServiceScopeFactory scopes, ILogger<SponsorLatePaymentSweeper> logger)
        {
            this.scopes = scopes;
            this.logger = logger;
        }

        /// <summary>One pass over the recent unpaid invoices.</summary>
        /// <returns>How many were accepted.</returns>
        public static async Task<int> SweepAsync(IServiceProvider services, DateTime nowUtc, ILogger logger, CancellationToken ct)
        {
            var invoices = services.GetRequiredService<ISponsoredListingInvoiceRepository>();
            var latePayments = services.GetRequiredService<ISponsorLatePaymentService>();

            var ids = await invoices.GetLatePaymentCandidateIdsAsync(nowUtc - Window, ct).ConfigureAwait(false);
            int accepted = 0;
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await latePayments.TryAcceptAsync(id, ct).ConfigureAwait(false) == LatePaymentOutcome.Accepted)
                    {
                        accepted++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One unreachable invoice must not stop the rest; it is looked at again next pass.
                    logger.LogWarning(ex, "Late payment sweep: could not check sponsorship invoice {Id}.", id);
                }
            }

            return accepted;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(StartDelay, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = this.scopes.CreateScope();
                        await SweepAsync(scope.ServiceProvider, DateTime.UtcNow, this.logger, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        this.logger.LogWarning(ex, "Late payment sweep failed; will retry.");
                    }

                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }
}
