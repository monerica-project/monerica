using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Constants;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Emails the listing owner once when a new review or non-owner reply on their listing goes
// live. Approval and delivery are decoupled: this job (every ~5 min) detects newly-approved
// items, enqueues an outbox row per item (once-only via unique indexes), then sends pending
// rows. Only items created AFTER the owner enabled notifications are considered (no backlog).
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";

var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(StringConstants.AppSettingsFileName, optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var profileLinkTemplate = config.GetValue<string>("ProfileLinkTemplate") ?? "https://monerica.com/site/[KEY]";
var adminLinkTemplate = config.GetValue<string>("AdminLinkTemplate") ?? "https://app.monerica.com/site/[KEY]/admin";
var maxAttempts = config.GetValue<int?>("MaxSendAttempts") ?? 5;

var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        DbProvider.Configure(options, config))
    .AddDbRepositories()
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
        return new EmailService(emailConfig, emailSettings, provider.GetRequiredService<IServiceScopeFactory>(), EmailSendSource.ReviewNotifier);
    })
    .BuildServiceProvider();

var emailService = serviceProvider.GetRequiredService<IEmailService>();

// Site name from config (ContentSnippet) so notification emails aren't hardcoded to "Monerica"
// — this job is shared by forks of the directory. Falls back to "Monerica" if unset.
string siteName;
using (var cfgScope = serviceProvider.CreateScope())
{
    siteName = cfgScope.ServiceProvider.GetRequiredService<IContentSnippetRepository>()
        .GetValue(SiteConfigSetting.SiteName);
}

if (string.IsNullOrWhiteSpace(siteName))
{
    siteName = "Monerica";
}

static bool IsValidEmail(string? e)
{
    if (string.IsNullOrWhiteSpace(e))
    {
        return false;
    }

    var t = e.Trim();
    try
    {
        return new System.Net.Mail.MailAddress(t).Address == t;
    }
    catch
    {
        return false;
    }
}

using (var scope = serviceProvider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // ---------- Phase 1: enqueue outbox rows for newly-approved items ----------
    var newReviews = await db.DirectoryEntryReviews
        .Include(r => r.DirectoryEntry)
        .Where(r => r.ModerationStatus == ReviewModerationStatus.Approved
            && r.DirectoryEntry != null
            && r.DirectoryEntry.ReviewEmailNotificationsEnabled
            && r.DirectoryEntry.Email != null
            && r.DirectoryEntry.ReviewEmailNotificationsEnabledUtc != null
            && r.CreateDate > r.DirectoryEntry.ReviewEmailNotificationsEnabledUtc
            && !db.ReviewNotifications.Any(n => n.DirectoryEntryReviewId == r.DirectoryEntryReviewId))
        .ToListAsync();

    foreach (var r in newReviews)
    {
        if (!IsValidEmail(r.DirectoryEntry!.Email))
        {
            continue;
        }

        db.ReviewNotifications.Add(new ReviewNotification
        {
            DirectoryEntryId = r.DirectoryEntryId,
            NotificationType = ReviewNotificationType.NewReview,
            DirectoryEntryReviewId = r.DirectoryEntryReviewId,
            RecipientEmail = r.DirectoryEntry.Email!.Trim(),
            Status = ReviewNotificationStatus.Pending,
            CreateDate = DateTime.UtcNow,
        });
    }

    var newReplies = await db.DirectoryEntryReviewComments
        .Include(c => c.DirectoryEntryReview!)
            .ThenInclude(r => r.DirectoryEntry)
        .Where(c => c.ModerationStatus == ReviewModerationStatus.Approved

            // Skip the owner's own replies. IsOwner is [NotMapped] (presentation-only), so we
            // key off the persisted CreatedByUserId that the owner-admin reply path stamps.
            && c.CreatedByUserId != "site-owner-admin"
            && c.DirectoryEntryReview != null
            && c.DirectoryEntryReview.DirectoryEntry != null
            && c.DirectoryEntryReview.DirectoryEntry.ReviewEmailNotificationsEnabled
            && c.DirectoryEntryReview.DirectoryEntry.Email != null
            && c.DirectoryEntryReview.DirectoryEntry.ReviewEmailNotificationsEnabledUtc != null
            && c.CreateDate > c.DirectoryEntryReview.DirectoryEntry.ReviewEmailNotificationsEnabledUtc
            && !db.ReviewNotifications.Any(n => n.DirectoryEntryReviewCommentId == c.DirectoryEntryReviewCommentId))
        .ToListAsync();

    foreach (var c in newReplies)
    {
        var entry = c.DirectoryEntryReview!.DirectoryEntry!;
        if (!IsValidEmail(entry.Email))
        {
            continue;
        }

        db.ReviewNotifications.Add(new ReviewNotification
        {
            DirectoryEntryId = entry.DirectoryEntryId,
            NotificationType = ReviewNotificationType.NewReply,
            DirectoryEntryReviewCommentId = c.DirectoryEntryReviewCommentId,
            RecipientEmail = entry.Email!.Trim(),
            Status = ReviewNotificationStatus.Pending,
            CreateDate = DateTime.UtcNow,
        });
    }

    await db.SaveChangesAsync();

    // ---------- Phase 2: send pending outbox rows ----------
    var pending = await db.ReviewNotifications
        .Include(n => n.DirectoryEntry)
        .Where(n => n.Status == ReviewNotificationStatus.Pending && n.AttemptCount < maxAttempts)
        .OrderBy(n => n.ReviewNotificationId)
        .ToListAsync();

    Console.WriteLine($"ReviewNotifier: queued {newReviews.Count} review(s) + {newReplies.Count} reply(ies); {pending.Count} pending to send.");

    foreach (var n in pending)
    {
        var entry = n.DirectoryEntry!;
        var key = entry.DirectoryEntryKey;
        var profileUrl = profileLinkTemplate.Replace("[KEY]", key);
        var adminUrl = adminLinkTemplate.Replace("[KEY]", key);
        var name = string.IsNullOrWhiteSpace(entry.Name) ? "your listing" : entry.Name;

        // The date the item was left, so the owner can tell notifications apart.
        DateTime? itemLeftUtc = n.NotificationType == ReviewNotificationType.NewReply && n.DirectoryEntryReviewCommentId != null
            ? await db.DirectoryEntryReviewComments
                .Where(c => c.DirectoryEntryReviewCommentId == n.DirectoryEntryReviewCommentId)
                .Select(c => (DateTime?)c.CreateDate).FirstOrDefaultAsync()
            : n.DirectoryEntryReviewId != null
                ? await db.DirectoryEntryReviews
                    .Where(r => r.DirectoryEntryReviewId == n.DirectoryEntryReviewId)
                    .Select(r => (DateTime?)r.CreateDate).FirstOrDefaultAsync()
                : null;
        var leftStr = itemLeftUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "unknown date";

        string subject;
        string plain;
        if (n.NotificationType == ReviewNotificationType.NewReply)
        {
            subject = $"New reply on your {siteName} listing: {name} (left {leftStr})";
            plain = $"Someone replied to a review on your {siteName} listing \"{name}\".\n\n"
                + $"Reply left: {leftStr}\n\n"
                + $"View your live listing: {profileUrl}\n"
                + $"Log in to view & respond (PGP): {adminUrl}\n";
        }
        else
        {
            subject = $"New review on your {siteName} listing: {name} (left {leftStr})";
            plain = $"Your {siteName} listing \"{name}\" received a new review.\n\n"
                + $"Review left: {leftStr}\n\n"
                + $"View your live listing: {profileUrl}\n"
                + $"Log in to view & respond (PGP): {adminUrl}\n";
        }

        var itemWord = n.NotificationType == ReviewNotificationType.NewReply ? "Reply" : "Review";
        var html = $"<p>{System.Net.WebUtility.HtmlEncode($"{itemWord} on your {siteName} listing: {name}")}</p>"
            + $"<p><strong>{itemWord} left:</strong> {leftStr}</p>"
            + $"<p><a href=\"{profileUrl}\">View your live listing</a><br />"
            + $"<a href=\"{adminUrl}\">Log in to view &amp; respond (PGP)</a></p>";

        try
        {
            n.AttemptCount++;
            await emailService.SendEmailAsync(subject, plain, html, new List<string> { n.RecipientEmail });
            n.Status = ReviewNotificationStatus.Sent;
            n.SentUtc = DateTime.UtcNow;
            n.UpdateDate = DateTime.UtcNow;
            n.LastError = null;
            Console.WriteLine($"  sent {n.NotificationType} #{n.ReviewNotificationId} to {n.RecipientEmail}");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            n.LastError = msg.Length > 1000 ? msg.Substring(0, 1000) : msg;
            if (n.AttemptCount >= maxAttempts)
            {
                n.Status = ReviewNotificationStatus.Failed;
            }

            n.UpdateDate = DateTime.UtcNow;
            Console.WriteLine($"  FAILED #{n.ReviewNotificationId} attempt {n.AttemptCount}: {msg}");
        }

        await db.SaveChangesAsync();
    }
}

Console.WriteLine("ReviewNotifier: done.");
