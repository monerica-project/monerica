using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using DirectoryManager.Utilities.Helpers;
using DirectoryManager.Web.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(StringConstants.AppSettingsFileName, optional: true, reloadOnChange: true)
    .Build();

// Email message key and notification link template from configuration
var emailMessageKey = config.GetValue<string>("EmailKeys:SponsoredListingOpeningNotification") ??
    throw new Exception("EmailKeys:SponsoredListingOpeningNotification is missing in configuration.");
var notificationLinkTemplate = config.GetValue<string>("NotificationLinkTemplate") ??
    throw new Exception("NotificationLinkTemplate is missing in configuration.");

// Register services in the service container
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(DirectoryManager.Data.Constants.StringConstants.DefaultConnection)))
    .AddRepositories() // Add repositories using the extension method
    .AddSingleton<IEmailService, EmailService>(provider =>
    {
        var emailConfig = new SendGridConfig
        {
            ApiKey = config["SendGrid:ApiKey"] ?? throw new InvalidOperationException("SendGrid:ApiKey is missing in configuration."),
            SenderEmail = config["SendGrid:SenderEmail"] ?? throw new InvalidOperationException("SendGrid:SenderEmail is missing in configuration."),
            SenderName = config["SendGrid:SenderName"] ?? "Default Sender Name" // Default value if SenderName is not provided.
        };

        return new EmailService(emailConfig);
    })
    .BuildServiceProvider();

// Retrieve required services
var notificationRepo = serviceProvider.GetRequiredService<ISponsoredListingOpeningNotificationRepository>();
var listingRepo = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
var directoryEntryRepository = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var sponsoredListingRepository = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
var reservationRepo = serviceProvider.GetRequiredService<ISponsoredListingReservationRepository>();
var emailMessageRepo = serviceProvider.GetRequiredService<IEmailMessageRepository>();
var emailService = serviceProvider.GetRequiredService<IEmailService>();

// Fetch the email message template
var emailMessage = emailMessageRepo.GetByKey(emailMessageKey);

if (emailMessage == null)
{
    Console.WriteLine($"Email template with key '{emailMessageKey}' not found. Exiting.");
    return;
}

// Determine if there is an opening for Main Sponsor
var mainSponsorType = SponsorshipType.MainSponsor;
var mainSponsorReservationGroup = ReservationGroupHelper.CreateReservationGroup(mainSponsorType, 0);
var currentMainSponsorListings = await listingRepo.GetAllActiveListingsAsync(mainSponsorType);

var hasOpeningForMainSponsor = false;

if (currentMainSponsorListings.Any())
{
    var activeCount = currentMainSponsorListings.Count();
    if (activeCount < IntegerConstants.MaxMainSponsoredListings)
    {
        var totalActiveListings = await listingRepo.GetActiveListingsCountAsync(mainSponsorType, null);
        var totalActiveReservations = await reservationRepo.GetActiveReservationsCountAsync(mainSponsorReservationGroup);

        hasOpeningForMainSponsor = CanPurchaseMainSponsorListing(totalActiveListings, totalActiveReservations, mainSponsorType);
    }
}
else
{
    hasOpeningForMainSponsor = true; // No active listings, opening exists.
}

// Fetch pending notifications
var pendingNotifications = await notificationRepo.GetPendingNotificationsAsync();

Console.WriteLine($"Found {pendingNotifications.Count()} pending notifications.");

// Process each notification
foreach (var notification in pendingNotifications)
{
    // Skip notifications for subcategories if the sponsorship type is Main Sponsor and no opening exists
    if (notification.SponsorshipType == SponsorshipType.MainSponsor && !hasOpeningForMainSponsor)
    {
        Console.WriteLine($"No opening for {SponsorshipType.MainSponsor}. Skipping notification for Email: {notification.Email}");
        continue;
    }

    if (notification.SponsorshipType == SponsorshipType.SubcategorySponsor)
    {
        bool canBuySubcategorySponsor = await CanPurchaseSubcategoryListing(directoryEntryRepository, sponsoredListingRepository, notification);

        if (!canBuySubcategorySponsor)
        {
            Console.WriteLine($"No opening for {SponsorshipType.SubcategorySponsor}. Skipping notification for Email: {notification.Email}");
            continue;
        }
    }

    // Generate the notification link using the template and replace placeholders
    var notificationLink = notificationLinkTemplate
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsorshipTypePlaceholder, notification.SponsorshipType.ToString())
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SubCategoryIdPlaceholder, notification.SubCategoryId?.ToString() ?? string.Empty);

    // Prepare the email content by replacing placeholders
    var plainTextContent = emailMessage.EmailBodyText
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsorshipTypePlaceholder, EnumHelper.GetDescription(notification.SponsorshipType))
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.ListingRenewalLinkToken, notificationLink);

    var htmlContent = emailMessage.EmailBodyHtml
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsorshipTypePlaceholder, EnumHelper.GetDescription(notification.SponsorshipType))
        .Replace(DirectoryManager.SponsoredListingReminder.Constants.StringConstants.ListingRenewalLinkToken, notificationLink);

    try
    {
        // Send the email using the EmailService
        await emailService.SendEmailAsync(emailMessage.EmailSubject, plainTextContent, htmlContent, new List<string> { notification.Email });

        //// Mark the notification as sent
        await notificationRepo.MarkReminderAsSentAsync(notification.SponsoredListingOpeningNotificationId);

        Console.WriteLine($"Notification sent and marked for Email: {notification.Email}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to send notification to Email: {notification.Email}. Error: {ex.Message}");
    }
}

Console.WriteLine("Processing complete.");

// Method to determine if a listing can be purchased
static bool CanPurchaseMainSponsorListing(int totalActiveListings, int totalActiveReservations, SponsorshipType sponsorshipType)
{
    // Logic to determine if the listing can be purchased
    return (totalActiveListings + totalActiveReservations) < IntegerConstants.MaxMainSponsoredListings;
}

static async Task<bool> CanPurchaseSubcategoryListing(
    IDirectoryEntryRepository directoryEntryRepository,
    ISponsoredListingRepository sponsoredListingRepository,
    DirectoryManager.Data.Models.SponsoredListings.SponsoredListingOpeningNotification notification)
{
    if (notification.SubCategoryId == null)
    {
        return false;
    }

    var totalActiveEntriesInCategory = await directoryEntryRepository
                                         .GetActiveEntriesByCategoryAsync(notification.SubCategoryId.Value);

    var totalActiveListings = await sponsoredListingRepository
                                .GetActiveListingsCountAsync(notification.SponsorshipType, notification.SubCategoryId.Value);

    var canBuySubcategorySponsor =
            totalActiveListings < IntegerConstants.MaxSubCategorySponsoredListings &&
            totalActiveEntriesInCategory.Count() >= IntegerConstants.MinimumSponsoredActiveSubcategories;
    return canBuySubcategorySponsor;
}