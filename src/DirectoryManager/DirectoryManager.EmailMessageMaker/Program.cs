using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.EmailMessageMaker.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static async Task Main(string[] args)
    {
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

        var emailCampaignKey = config["EmailCampaignKey"];
        if (string.IsNullOrEmpty(emailCampaignKey))
        {
            Console.WriteLine("Error: EmailCampaignKey is not configured.");
            return;
        }

        // Register services in the service container
        var serviceProvider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString(DirectoryManager.Data.Constants.StringConstants.DefaultConnection)))
            .AddDbRepositories()
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var directoryRepo = serviceProvider.GetRequiredService<IDirectoryEntryRepository>();
        var sponsoredListingRepository = serviceProvider.GetRequiredService<ISponsoredListingRepository>();
        var contentSnippetRepository = serviceProvider.GetRequiredService<IContentSnippetRepository>();
        var emailCampaignRepository = serviceProvider.GetRequiredService<IEmailCampaignRepository>();
        var emailMessageRepository = serviceProvider.GetRequiredService<IEmailMessageRepository>();
        var emailCampaignMessageRepository = serviceProvider.GetRequiredService<IEmailCampaignMessageRepository>();

        // Fetch new entries from the last week
        var weeklyResults = await directoryRepo.GetEntriesCreatedForPreviousWeekWithWeekKeyAsync();

        if (weeklyResults == null || !weeklyResults.Entries.Any())
        {
            Console.WriteLine("No new entries this week. Exiting.");
            return;
        }

        // Fetch all active sponsors
        var allSponsors = await sponsoredListingRepository.GetAllActiveSponsorsAsync();

        // Separate main sponsors and subcategory sponsors
        var mainSponsors = allSponsors.Where(s => s.SponsorshipType == SponsorshipType.MainSponsor)
                                       .OrderByDescending(s => s.CampaignEndDate)
                                       .ToList();

        var subCategorySponsors = allSponsors.Where(s => s.SponsorshipType == SponsorshipType.SubcategorySponsor)
                                             .OrderBy(s => s.DirectoryEntry?.SubCategory?.Category?.Name)
                                             .ThenBy(s => s.DirectoryEntry?.SubCategory?.Name)
                                             .ThenBy(s => s.DirectoryEntry?.Name)
                                             .ToList();

        var categorySponsors = allSponsors.Where(s => s.SponsorshipType == SponsorshipType.CategorySponsor)
                                     .OrderBy(s => s.DirectoryEntry?.SubCategory?.Category?.Name)
                                     .ThenBy(s => s.DirectoryEntry?.SubCategory?.Name)
                                     .ThenBy(s => s.DirectoryEntry?.Name)
                                     .ToList();

        // Fetch footer content
        var emailSettingUnsubscribeFooterHtml = contentSnippetRepository.GetValue(SiteConfigSetting.EmailSettingUnsubscribeFooterHtml);
        var emailSettingUnsubscribeFooterText = contentSnippetRepository.GetValue(SiteConfigSetting.EmailSettingUnsubscribeFooterText);
        var link2Name = contentSnippetRepository.GetValue(SiteConfigSetting.Link2Name);
        var link3Name = contentSnippetRepository.GetValue(SiteConfigSetting.Link3Name);
        var siteName = contentSnippetRepository.GetValue(SiteConfigSetting.SiteName);
        var rootUrl = contentSnippetRepository.GetValue(SiteConfigSetting.FullRootUrl);

        // Generate the final email HTML
        var emailHtml = MessageFormatHelper.GenerateHtmlEmail(
            weeklyResults.Entries,
            mainSponsors,
            categorySponsors,
            subCategorySponsors,
            rootUrl,
            siteName,
            emailSettingUnsubscribeFooterHtml,
            link2Name,
            link3Name);

        var emailText = MessageFormatHelper.GenerateTextEmail(
            weeklyResults.Entries,
            mainSponsors,
            categorySponsors,
            subCategorySponsors,
            emailSettingUnsubscribeFooterText);

        // Output HTML for debugging or further processing
        Console.WriteLine("Generated HTML Email Content:");
        Console.WriteLine(emailHtml);

        var emailCampaign = emailCampaignRepository.GetByKey(emailCampaignKey);

        if (emailCampaign == null)
        {
            Console.WriteLine($"No campaign for: {emailCampaignKey}");
            return;
        }

        var emailMessage = emailMessageRepository.GetByKey(weeklyResults.WeekStartDate);

        if (emailMessage != null)
        {
            Console.WriteLine($"Message exists with key: {weeklyResults.WeekStartDate}");
            return;
        }

        var weeklyEmailMessage = emailMessageRepository.Create(new DirectoryManager.Data.Models.Emails.EmailMessage()
        {
            EmailBodyHtml = emailHtml,
            EmailKey = weeklyResults.WeekStartDate,
            EmailBodyText = emailText,
            EmailSubject = $"Newest Entries For Week Of: {weeklyResults.WeekStartDate}",
        });

        emailCampaignMessageRepository.Create(new DirectoryManager.Data.Models.Emails.EmailCampaignMessage()
        {
            EmailCampaignId = emailCampaign.EmailCampaignId,
            EmailMessageId = weeklyEmailMessage.EmailMessageId,
            SequenceOrder = -1
        });

        Console.WriteLine("\nProcessing completed successfully.");
    }
}