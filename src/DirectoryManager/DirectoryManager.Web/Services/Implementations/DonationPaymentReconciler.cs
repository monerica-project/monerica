using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Services.Interfaces;

namespace DirectoryManager.Web.Services.Implementations
{
    /// <summary>
    /// Safety net for donation payments (submissions, verification requests, problem reports):
    /// every few minutes asks BTCPay about donations not yet marked paid and records the paid ones.
    /// So a missing/failed webhook can never leave a paid donation showing as unpaid.
    /// The first run after start checks every unpaid donation ever made; later runs check the
    /// last <see cref="RecentWindow"/> (late payments still land well inside that).
    /// </summary>
    public class DonationPaymentReconciler : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(14);
        private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(45);

        private readonly IServiceScopeFactory scopes;
        private readonly ILogger<DonationPaymentReconciler> logger;

        public DonationPaymentReconciler(IServiceScopeFactory scopes, ILogger<DonationPaymentReconciler> logger)
        {
            this.scopes = scopes;
            this.logger = logger;
        }

        /// <summary>One pass: check the given unpaid invoices and record the paid ones.</summary>
        /// <returns>How many were newly recorded as paid.</returns>
        public static async Task<int> ReconcileAsync(IServiceProvider services, DateTime? createdSinceUtc, ILogger logger, CancellationToken ct)
        {
            var submissions = services.GetRequiredService<ISubmissionRepository>();
            var verifications = services.GetRequiredService<IVerificationRequestRepository>();
            var problems = services.GetRequiredService<IProblemReportRepository>();
            var donations = services.GetRequiredService<IDonationPaymentService>();

            var invoiceIds = (await submissions.GetUnpaidInvoiceIdsAsync(createdSinceUtc, ct))
                .Concat(await verifications.GetUnpaidInvoiceIdsAsync(createdSinceUtc, ct))
                .Concat(await problems.GetUnpaidInvoiceIdsAsync(createdSinceUtc, ct))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            int recorded = 0;
            foreach (var invoiceId in invoiceIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await donations.RecordPaymentAsync(invoiceId, ct))
                    {
                        recorded++;
                        logger.LogInformation("Donation reconciler: invoice {InvoiceId} recorded as paid.", invoiceId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One unreachable/odd invoice must not stop the rest; it is retried next pass.
                    logger.LogWarning(ex, "Donation reconciler: could not check invoice {InvoiceId}.", invoiceId);
                }
            }

            return recorded;
        }

        /// <summary>Fills in the XMR received for paid donations that don't have it recorded yet.</summary>
        /// <returns>How many were filled in.</returns>
        public static async Task<int> BackfillPaidXmrAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
        {
            var donations = services.GetRequiredService<IDonationPaymentService>();
            var invoiceIds = (await services.GetRequiredService<ISubmissionRepository>().GetPaidInvoiceIdsMissingXmrAsync(ct))
                .Concat(await services.GetRequiredService<IVerificationRequestRepository>().GetPaidInvoiceIdsMissingXmrAsync(ct))
                .Concat(await services.GetRequiredService<IProblemReportRepository>().GetPaidInvoiceIdsMissingXmrAsync(ct))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            int filled = 0;
            foreach (var invoiceId in invoiceIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await donations.RecordPaidXmrAsync(invoiceId, ct))
                    {
                        filled++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Donation reconciler: could not read XMR paid for invoice {InvoiceId}.", invoiceId);
                }
            }

            return filled;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bool firstRun = true;
            try
            {
                await Task.Delay(StartDelay, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = this.scopes.CreateScope();
                        DateTime? since = firstRun ? null : DateTime.UtcNow - RecentWindow;
                        var recorded = await ReconcileAsync(scope.ServiceProvider, since, this.logger, stoppingToken);
                        if (firstRun)
                        {
                            await BackfillPaidXmrAsync(scope.ServiceProvider, this.logger, stoppingToken);
                        }

                        if (recorded > 0 || firstRun)
                        {
                            this.logger.LogInformation(
                                "Donation reconciler: {Recorded} newly paid ({Scope}).", recorded, firstRun ? "all unpaid" : "recent");
                        }

                        firstRun = false;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        this.logger.LogWarning(ex, "Donation reconciler pass failed; will retry.");
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
