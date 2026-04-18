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

const string SponsorshipTypePlaceholder = "[SPONSORSHIP_TYPE]";
const string SubCategoryIdPlaceholder = "[SUBCATEGORY_ID]";
const string CategoryIdPlaceholder = "[CATEGORY_ID]";
const string DirectoryEntryIdPlaceholder = "[DIRECTORY_ENTRY_ID]";

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile(StringConstants.AppSettingsFileName, optional: true, reloadOnChange: true)
    .Build();

// Email message key and notification link templates from configuration
var emailMessageKey = config.GetValue<string>("EmailKeys:SponsoredListingOpeningNotification") ??
    throw new Exception("EmailKeys:SponsoredListingOpeningNotification is missing in configuration.");

var notificationLinkTemplateDefault = config.GetValue<string>("NotificationLinkTemplateDefault") ??
    throw new Exception("NotificationLinkTemplateDefault is missing in configuration.");

var notificationLinkTemplateWithListing = config.GetValue<string>("NotificationLinkTemplateWithListing") ??
    notificationLinkTemplateDefault;

// Register services
var serviceProvider = new ServiceCollection()
    .AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(config.GetConnectionString(DirectoryManager.Data.Constants.StringConstants.DefaultConnection)))
    .AddDbRepositories()
    .AddSingleton<IEmailService, EmailService>(provider =>
    {
        using var scope = provider.CreateScope();
        var contentSnippetRepo = scope.ServiceProvider.GetRequiredService<IContentSnippetRepository>();

        var emailConfig = new SendGridConfig
        {
            ApiKey = contentSnippetRepo.GetValue(SiteConfigSetting.SendGridApiKey),
            SenderEmail = contentSnippetRepo.GetValue(SiteConfigSetting.SendGridSenderEmail),
            SenderName = contentSnippetRepo.GetValue(SiteConfigSetting.SendGridSenderName)
        };

        var emailSettings = new EmailSettings
        {
            UnsubscribeUrlFormat = contentSnippetRepo.GetValue(SiteConfigSetting.EmailSettingUnsubscribeUrlFormat),
            UnsubscribeEmail = contentSnippetRepo.GetValue(SiteConfigSetting.EmailSettingUnsubscribeEmail),
        };

        return new EmailService(emailConfig, emailSettings);
    })
    .BuildServiceProvider();

// Services
var notificationRepo = serviceProvider.GetRequiredService<ISponsoredListingOpeningNotificationRepository>();
var listingRepo = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
var directoryEntryRepository = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
var sponsoredListingRepository = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
var reservationRepo = serviceProvider.GetRequiredService<ISponsoredListingReservationRepository>();
var emailMessageRepo = serviceProvider.GetRequiredService<IEmailMessageRepository>();
var emailService = serviceProvider.GetRequiredService<IEmailService>();

// Fetch template
var emailMessage = emailMessageRepo.GetByKey(emailMessageKey);
if (emailMessage == null)
{
    Console.WriteLine($"Email template with key '{emailMessageKey}' not found. Exiting.");
    return;
}

// Determine if there is an opening for Main Sponsor
var mainSponsorType = SponsorshipType.MainSponsor;
var mainSponsorReservationGroup = ReservationGroupHelper.BuildReservationGroupName(mainSponsorType);
var currentMainSponsorListings = await listingRepo.GetActiveSponsorsByTypeAsync(mainSponsorType);

var hasOpeningForMainSponsor = false;

if (currentMainSponsorListings.Any())
{
    var totalActiveListings = await listingRepo.GetActiveSponsorsCountAsync(mainSponsorType, null);
    var totalActiveReservations = await reservationRepo.GetActiveReservationsCountAsync(mainSponsorReservationGroup);

    hasOpeningForMainSponsor = CanPurchaseMainSponsorListing(totalActiveListings, totalActiveReservations);
}
else
{
    hasOpeningForMainSponsor = true;
}

// Fetch pending notifications
var pendingNotifications = await notificationRepo.GetSubscribers();
Console.WriteLine($"Found {pendingNotifications.Count()} pending notifications.");

// Process each notification
foreach (var notification in pendingNotifications)
{
    try
    {
        // MAIN: skip if no opening
        if (notification.SponsorshipType == SponsorshipType.MainSponsor && !hasOpeningForMainSponsor)
        {
            Console.WriteLine($"No opening for {SponsorshipType.MainSponsor}. Skipping: {notification.Email}");
            continue;
        }

        // CATEGORY/SUBCATEGORY: validate opening (includes reservations)
        if (notification.SponsorshipType == SponsorshipType.CategorySponsor)
        {
            var ok = await CanPurchaseCategoryListing(directoryEntryRepository, sponsoredListingRepository, reservationRepo, notification);
            if (!ok)
            {
                Console.WriteLine($"No opening for {SponsorshipType.CategorySponsor}. Skipping: {notification.Email}");
                continue;
            }
        }

        if (notification.SponsorshipType == SponsorshipType.SubcategorySponsor)
        {
            var ok = await CanPurchaseSubcategoryListing(directoryEntryRepository, sponsoredListingRepository, reservationRepo, notification);
            if (!ok)
            {
                Console.WriteLine($"No opening for {SponsorshipType.SubcategorySponsor}. Skipping: {notification.Email}");
                continue;
            }
        }

        // Choose template: with listing if directoryEntryId exists AND entry still exists
        var useWithListing = false;

        if (notification.DirectoryEntryId.HasValue && notification.DirectoryEntryId.Value > 0)
        {
            var entry = await directoryEntryRepository.GetByIdAsync(notification.DirectoryEntryId.Value);
            useWithListing = entry != null;
        }

        var templateToUse = useWithListing ? notificationLinkTemplateWithListing : notificationLinkTemplateDefault;

        // Build link
        var notificationLink = BuildNotificationLink(templateToUse, notification);

        // Prepare email body
        var plainTextContent = emailMessage.EmailBodyText
            .Replace(
                DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsorshipTypePlaceholder,
                EnumHelper.GetDescription(notification.SponsorshipType))
            .Replace(
                DirectoryManager.SponsoredListingReminder.Constants.StringConstants.ListingRenewalLinkToken,
                notificationLink);

        var htmlContent = emailMessage.EmailBodyHtml
            .Replace(
                DirectoryManager.SponsoredListingReminder.Constants.StringConstants.SponsorshipTypePlaceholder,
                EnumHelper.GetDescription(notification.SponsorshipType))
            .Replace(
                DirectoryManager.SponsoredListingReminder.Constants.StringConstants.ListingRenewalLinkToken,
                notificationLink);

        // Send
        await emailService.SendEmailAsync(
            emailMessage.EmailSubject,
            plainTextContent,
            htmlContent,
            new List<string> { notification.Email });

        // Mark as sent + store sent link
        await notificationRepo.MarkReminderAsSentAsync(notification.SponsoredListingOpeningNotificationId, notificationLink);

        Console.WriteLine($"Notification sent and marked for: {notification.Email}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to notify {notification.Email}. Error: {ex.Message}");
    }
}

Console.WriteLine("Processing complete.");

static string BuildNotificationLink(
    string template,
    DirectoryManager.Data.Models.SponsoredListings.SponsoredListingOpeningNotification notification)
{
    // Fill placeholders based on sponsorship type
    var sponsorshipType = notification.SponsorshipType.ToString();

    var categoryId = "";
    var subCategoryId = "";

    if (notification.SponsorshipType == SponsorshipType.CategorySponsor)
    {
        categoryId = notification.TypeId?.ToString() ?? "";
    }
    else if (notification.SponsorshipType == SponsorshipType.SubcategorySponsor)
    {
        subCategoryId = notification.TypeId?.ToString() ?? "";
    }

    var directoryEntryId = (notification.DirectoryEntryId.HasValue && notification.DirectoryEntryId.Value > 0)
        ? notification.DirectoryEntryId.Value.ToString()
        : "";

    return template
        .Replace(SponsorshipTypePlaceholder, sponsorshipType)
        .Replace(CategoryIdPlaceholder, categoryId)
        .Replace(SubCategoryIdPlaceholder, subCategoryId)
        .Replace(DirectoryEntryIdPlaceholder, directoryEntryId);
}

static bool CanPurchaseMainSponsorListing(int totalActiveListings, int totalActiveReservations)
{
    return (totalActiveListings + totalActiveReservations) < IntegerConstants.MaxMainSponsoredListings;
}

static async Task<bool> CanPurchaseCategoryListing(
    IDirectoryEntryRepository directoryEntryRepository,
    ISponsoredListingRepository sponsoredListingRepository,
    ISponsoredListingReservationRepository reservationRepo,
    DirectoryManager.Data.Models.SponsoredListings.SponsoredListingOpeningNotification notification)
{
    if (!notification.TypeId.HasValue || notification.TypeId.Value <= 0)
    {
        return false;
    }

    var categoryId = notification.TypeId.Value;

    // Minimum listings requirement for category sponsor
    var totalActiveEntriesInCategory = await directoryEntryRepository.GetActiveEntriesByCategoryAsync(categoryId);

    // Active sponsors in that category
    var totalActiveListings = await sponsoredListingRepository.GetActiveSponsorsCountAsync(notification.SponsorshipType, categoryId);

    // Active reservations in that category pool
    var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(notification.SponsorshipType, categoryId);
    var totalActiveReservations = await reservationRepo.GetActiveReservationsCountAsync(reservationGroup);

    var capacityOk = (totalActiveListings + totalActiveReservations) < IntegerConstants.MaxCategorySponsoredListings;
    var hasEnoughListings = totalActiveEntriesInCategory.Count() >= IntegerConstants.MinRequiredCategories;

    return capacityOk && hasEnoughListings;
}

static async Task<bool> CanPurchaseSubcategoryListing(
    IDirectoryEntryRepository directoryEntryRepository,
    ISponsoredListingRepository sponsoredListingRepository,
    ISponsoredListingReservationRepository reservationRepo,
    DirectoryManager.Data.Models.SponsoredListings.SponsoredListingOpeningNotification notification)
{
    if (!notification.TypeId.HasValue || notification.TypeId.Value <= 0)
    {
        return false;
    }

    var subCategoryId = notification.TypeId.Value;

    // Minimum listings requirement for subcategory sponsor
    var totalActiveEntriesInSubcategory = await directoryEntryRepository.GetActiveEntriesBySubcategoryAsync(subCategoryId);

    // Active sponsors in that subcategory
    var totalActiveListings = await sponsoredListingRepository.GetActiveSponsorsCountAsync(notification.SponsorshipType, subCategoryId);

    // Active reservations in that subcategory pool
    var reservationGroup = ReservationGroupHelper.BuildReservationGroupName(notification.SponsorshipType, subCategoryId);
    var totalActiveReservations = await reservationRepo.GetActiveReservationsCountAsync(reservationGroup);

    var capacityOk = (totalActiveListings + totalActiveReservations) < IntegerConstants.MaxSubcategorySponsoredListings;
    var hasEnoughListings = totalActiveEntriesInSubcategory.Count() >= IntegerConstants.MinRequiredSubcategories;

    return capacityOk && hasEnoughListings;
}
