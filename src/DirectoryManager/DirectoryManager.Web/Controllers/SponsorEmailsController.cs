using System.Globalization;
using System.Text;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.Models.Sponsorship;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace DirectoryManager.Web.Controllers
{
    [Authorize]
    public class SponsorEmailsController : Controller
    {
        private readonly ISponsoredListingInvoiceRepository invoiceRepository;

        public SponsorEmailsController(ISponsoredListingInvoiceRepository invoiceRepository)
        {
            this.invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
        }

        [HttpGet("sponsoremails")]
        public async Task<IActionResult> Index(bool paidOnly = true)
        {
            var rows = await this.BuildRowsAsync(paidOnly).ConfigureAwait(false);

            var emails = rows.Select(r => r.Email).ToList();

            var vm = new SponsorEmailsViewModel
            {
                PaidOnly = paidOnly,
                Rows = rows,
                EmailsNewlineSeparated = string.Join("\n", emails),
                EmailsCommaSeparated = string.Join(", ", emails)
            };

            return this.View(vm);
        }

        [HttpGet("sponsoremails/download-csv")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> DownloadCsv(bool paidOnly = true)
        {
            var rows = await this.BuildRowsAsync(paidOnly).ConfigureAwait(false);

            var fileName = $"SponsorEmails_{(paidOnly ? "PAID" : "ALL")}_{DateTime.UtcNow:yyyyMMdd}.csv";

            this.Response.ContentType = "text/csv; charset=utf-8";
            this.Response.Headers[HeaderNames.ContentDisposition] = $"attachment; filename=\"{fileName}\"";
            this.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            this.Response.Headers.Pragma = "no-cache";
            this.Response.Headers["X-Download-Options"] = "noopen";

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var bom = utf8.GetPreamble();
            if (bom.Length > 0)
            {
                await this.Response.Body.WriteAsync(bom, 0, bom.Length).ConfigureAwait(false);
            }

            await using var writer = new StreamWriter(this.Response.Body, utf8, bufferSize: 64 * 1024, leaveOpen: true);

            static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
            static string D(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            static string Q(string? s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";

            try
            {
                await writer.WriteLineAsync("Email,Listing,Invoices,Paid Invoices,Total Paid,Last Invoice Date,Last Campaign End")
                            .ConfigureAwait(false);

                foreach (var r in rows)
                {
                    await writer.WriteLineAsync(string.Join(
                        ",",
                        Q(r.Email),
                        Q(r.ListingName),
                        r.InvoiceCount.ToString(CultureInfo.InvariantCulture),
                        r.PaidInvoiceCount.ToString(CultureInfo.InvariantCulture),
                        Money(r.TotalPaid),
                        D(r.LastInvoiceDate),
                        D(r.LastCampaignEndDate))).ConfigureAwait(false);
                }

                await writer.FlushAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            { /* client canceled */
            }
            catch (IOException)
            { /* broken pipe */
            }

            return new EmptyResult();
        }

        /// <summary>
        /// Loads every invoice, optionally restricts to paid ones, and collapses to one row
        /// per distinct (case-insensitive, trimmed) email address.
        /// </summary>
        private async Task<IReadOnlyList<SponsorEmailRow>> BuildRowsAsync(bool paidOnly)
        {
            var invoices = await this.invoiceRepository.GetAllAsync().ConfigureAwait(false);

            var withEmail = invoices.Where(i => !string.IsNullOrWhiteSpace(i.Email));

            if (paidOnly)
            {
                withEmail = withEmail.Where(i => i.PaymentStatus == PaymentStatus.Paid);
            }

            return withEmail
                .GroupBy(i => i.Email!.Trim().ToLowerInvariant())
                .Select(g =>
                {
                    var ordered = g.OrderByDescending(i => i.CreateDate).ToList();
                    var latest = ordered[0];
                    var paid = ordered.Where(i => i.PaymentStatus == PaymentStatus.Paid).ToList();

                    return new SponsorEmailRow
                    {
                        Email = latest.Email!.Trim(),
                        ListingName = latest.DirectoryEntry?.Name ?? string.Empty,
                        InvoiceCount = ordered.Count,
                        PaidInvoiceCount = paid.Count,
                        TotalPaid = paid.Sum(i => i.Amount),
                        LastInvoiceDate = latest.CreateDate,
                        LastCampaignEndDate = ordered.Max(i => i.CampaignEndDate)
                    };
                })
                .OrderByDescending(r => r.LastInvoiceDate)
                .ToList();
        }
    }
}
