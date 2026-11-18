using System.Net;
using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Helpers;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Constants;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tokens = DirectoryManager.SponsoredListingReminder.Constants.StringConstants;
using SendGrid.Helpers.Mail;

// Build configuration. Layer order matters — later sources override earlier ones:
//   1. appsettings.json            (base, committed to git, may have empty placeholders)
//   2. appsettings.{env}.json      (deploy-time overlay with real connection string,
//                                   written by deploy-jobs.sh from deploy-config.sh)
//   3. environment variables       (allows ConnectionStrings__DefaultConnection or
//                                   any other key to override at runtime)
//
// The default ConfigurationBuilder does NOT auto-load the environment overlay the way
// ASP.NET's Host.CreateDefaultBuilder does — we have to add it explicitly. systemd
// sets DOTNET_ENVIRONMENT=Production for the unit; we read that here.
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(StringConstants.AppSettingsFileName, optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Get the default expiration time span from configuration
int hours = config.GetValue<int?>("ExpirationHours") ?? DirectoryManager.SponsoredListingReminder.Constants.IntegerConstants.DefaultHours;
var expirationTimeSpan = TimeSpan.FromHours(hours);

// Email message key and renewal link template from configuration
var emailMessageKey = config.GetValue<string>("EmailKeys:SponsoredListingExpirationReminder") ??
    DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsoredListingExpirationReminder;
var finalMessageKey = config.GetValue<string>("EmailKeys:SponsoredListingFinalReminder") ?? Tokens.SponsoredListingFinalReminder;
int finalHours = config.GetValue<int?>("FinalReminderHours") ?? SponsorReminderPlan.FinalWindowHours;
var renewalLinkTemplate = config.GetValue<string>("RenewalLinkTemplate")
    ?? throw new Exception("RenewalLinkTemplate is missing in configuration.");

// Register services in the service container
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        DirectoryManager.Data.DbContextInfo.DbProvider.Configure(options, config))
    .AddDbRepositories() // Add repositories using the extension method
    .AddSingleton<IEmailService, EmailService>(provider =>
    {
        using var scope = provider.CreateScope();
        var contentSnippetRepo = scope.ServiceProvider.GetRequiredService<IContentSnippetRepository>();

        var emailConfig = config.GetSection("SendGrid").Get<SendGridConfig>()
                    ?? throw new Exception("SendGrid configuration section is missing. Add a \"SendGrid\" section to appsettings.");

        var emailSettings = new EmailSettings
        {
            UnsubscribeUrlFormat = contentSnippetRepo.GetValue(SiteConfigSetting.EmailSettingUnsubscribeUrlFormat),
            UnsubscribeEmail = contentSnippetRepo.GetValue(SiteConfigSetting.EmailSettingUnsubscribeEmail),
        };

        return new EmailService(emailConfig, emailSettings, provider.GetRequiredService<IServiceScopeFactory>(), EmailSendSource.SponsoredListingReminder);
    })
    .BuildServiceProvider();

// Retrieve required services
var listingRepo = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
var emailMessageRepo = serviceProvider.GetRequiredService<IEmailMessageRepository>();
var emailService = serviceProvider.GetRequiredService<IEmailService>();
var db = serviceProvider.GetRequiredService<ApplicationDbContext>();

// Two emails per sponsorship period, each sent at most once:
//   first  - when the sponsorship ends within ExpirationHours (72)
//   final  - when FinalReminderHours (24) or less are left and it has still not been renewed
// "Sent" is recorded on the period's invoice (ReminderSentUtc / FinalReminderSentUtc). A renewal
// extends the listing and attaches a NEW invoice, so a renewed sponsor drops out of both windows
// and the next period starts clean. The job CLAIMS the timestamp before sending (an UPDATE that
// only succeeds while it is still empty) and releases it if the send fails, so neither a crash
// nor two runs at once can send the same email twice. Every send is also in the email log.
var firstMessage = emailMessageRepo.GetByKey(emailMessageKey);
var finalMessage = emailMessageRepo.GetByKey(finalMessageKey);

if (firstMessage == null)
{
    Console.WriteLine($"Email template with key '{emailMessageKey}' not found. Exiting.");
    return;
}

if (finalMessage == null)
{
    Console.WriteLine($"Email template with key '{finalMessageKey}' not found: final reminders are skipped this run.");
}

var expiringListings = await listingRepo.GetExpiringSponsorsWithinTimeAsync(expirationTimeSpan);

Console.WriteLine($"Found {expiringListings.Count()} listings expiring within {hours} hours.");

// One failure (bad address, SendGrid hiccup, DB error) is logged and the batch continues.
foreach (var listing in expiringListings)
{
    try
    {
        var invoiceId = listing.SponsoredListingInvoiceId;
        var invoice = await db.SponsoredListingInvoices.AsNoTracking()
            .Where(i => i.SponsoredListingInvoiceId == invoiceId)
            .Select(i => new { i.Email, i.IsReminderSent, i.ReminderSentUtc, i.FinalReminderSentUtc })
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            Console.WriteLine($"No invoice found for Listing ID: {listing.SponsoredListingId}");
            continue;
        }

        if (string.IsNullOrWhiteSpace(invoice.Email))
        {
            Console.WriteLine($"No email associated with Invoice ID: {invoiceId}");
            continue;
        }

        // Whole seconds, so the stored value compares equal when a failed send releases the claim
        // (the database keeps microseconds, .NET keeps finer ticks).
        var now = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var kind = SponsorReminderPlan.Due(now, listing.CampaignEndDate, invoice.IsReminderSent, invoice.ReminderSentUtc, invoice.FinalReminderSentUtc, hours, finalHours);
        if (kind == SponsorReminderKind.None || (kind == SponsorReminderKind.Final && finalMessage == null))
        {
            Console.WriteLine($"Nothing due for Invoice ID: {invoiceId} (first sent: {invoice.ReminderSentUtc?.ToString("u") ?? (invoice.IsReminderSent ? "yes" : "no")}, final sent: {invoice.FinalReminderSentUtc?.ToString("u") ?? "no"})");
            continue;
        }

        // Claim first, then send.
        int claimed = kind == SponsorReminderKind.Final
            ? await db.SponsoredListingInvoices
                .Where(i => i.SponsoredListingInvoiceId == invoiceId && i.FinalReminderSentUtc == null)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.FinalReminderSentUtc, now).SetProperty(i => i.IsReminderSent, true))
            : await db.SponsoredListingInvoices
                .Where(i => i.SponsoredListingInvoiceId == invoiceId && i.ReminderSentUtc == null && !i.IsReminderSent)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.ReminderSentUtc, now).SetProperty(i => i.IsReminderSent, true));
        if (claimed != 1)
        {
            Console.WriteLine($"{kind} reminder for Invoice ID {invoiceId} was already taken by another run. Skipped.");
            continue;
        }

        try
        {
            var template = kind == SponsorReminderKind.Final ? finalMessage! : firstMessage;
            var renewalLink = renewalLinkTemplate
                .Replace(Tokens.DirectoryEntryIdPlaceholder, listing.DirectoryEntryId.ToString())
                .Replace(Tokens.SponsorshipTypePlaceholder, listing.SponsorshipType.ToString());
            var listingName = listing.DirectoryEntry?.Name ?? Tokens.DefaultListingName;
            var hoursLeft = SponsorReminderPlan.HoursLeft(now, listing.CampaignEndDate).ToString();
            var expiresAt = listing.CampaignEndDate.ToString("d MMMM yyyy 'at' HH:mm", System.Globalization.CultureInfo.InvariantCulture);

            // "main sponsor", "category sponsor (Exchanges)", "subcategory sponsor (VPNs)"
            string placement = "main sponsor";
            int? typeId = null;
            if (listing.SponsorshipType == SponsorshipType.CategorySponsor)
            {
                typeId = listing.CategoryId;
                var name = await db.Categories.Where(c => c.CategoryId == typeId).Select(c => c.Name).FirstOrDefaultAsync();
                placement = string.IsNullOrWhiteSpace(name) ? "category sponsor" : $"category sponsor ({name})";
            }
            else if (listing.SponsorshipType == SponsorshipType.SubcategorySponsor)
            {
                typeId = listing.SubCategoryId;
                var name = await db.Subcategories.Where(c => c.SubCategoryId == typeId).Select(c => c.Name).FirstOrDefaultAsync();
                placement = string.IsNullOrWhiteSpace(name) ? "subcategory sponsor" : $"subcategory sponsor ({name})";
            }

            // Other advertisers who asked to be told when this exact spot opens (the sponsor's own alert does not count).
            var sponsorEmail = invoice.Email.Trim().ToLower();
            var waiting = await db.SponsoredListingOpeningNotifications
                .Where(n => n.IsActive && !n.IsReminderSent && n.SponsorshipType == listing.SponsorshipType && n.TypeId == typeId)
                .Select(n => n.Email.ToLower())
                .Distinct()
                .CountAsync(e => e != sponsorEmail);
            var waitlistLine = waiting switch
            {
                0 => string.Empty,
                1 => "1 advertiser has asked to be notified when this spot opens.",
                _ => $"{waiting} advertisers have asked to be notified when this spot opens.",
            };

            string Fill(string text, bool html)
            {
                string V(string v) => html ? WebUtility.HtmlEncode(v) : v;
                return text
                    .Replace(Tokens.ListingNameToken, V(listingName))
                    .Replace(Tokens.ListingRenewalLinkToken, V(renewalLink))
                    .Replace(Tokens.ExpiresAtToken, V(expiresAt))
                    .Replace(Tokens.HoursLeftToken, hoursLeft)
                    .Replace(Tokens.PlacementToken, V(placement))
                    .Replace(" " + Tokens.WaitlistLineToken, waitlistLine.Length == 0 ? string.Empty : " " + V(waitlistLine))
                    .Replace(Tokens.WaitlistLineToken, V(waitlistLine));
            }

            await emailService.SendEmailAsync(Fill(template.EmailSubject, false), Fill(template.EmailBodyText, false), Fill(template.EmailBodyHtml, true), new List<string> { invoice.Email });
            Console.WriteLine($"{kind} reminder sent for Invoice ID {invoiceId} ({listingName}, {placement}): {hoursLeft} hours left, ends {expiresAt} UTC, {waiting} waiting.");
        }
        catch
        {
            // The email did not go out: give the claim back so the next run tries again.
            if (kind == SponsorReminderKind.Final)
            {
                await db.SponsoredListingInvoices
                    .Where(i => i.SponsoredListingInvoiceId == invoiceId && i.FinalReminderSentUtc == now)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.FinalReminderSentUtc, (DateTime?)null).SetProperty(i => i.IsReminderSent, invoice.IsReminderSent));
            }
            else
            {
                await db.SponsoredListingInvoices
                    .Where(i => i.SponsoredListingInvoiceId == invoiceId && i.ReminderSentUtc == now)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.ReminderSentUtc, (DateTime?)null).SetProperty(i => i.IsReminderSent, false));
            }

            throw;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send reminder for Listing ID {listing.SponsoredListingId}: {ex.Message}");
    }
}

Console.WriteLine("Processing complete.");
