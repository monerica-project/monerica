using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Services.Implementations;
using DirectoryManager.NewsletterSender.Services.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
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

        // Register services in the service container
        var serviceProvider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString(DirectoryManager.Data.Constants.StringConstants.DefaultConnection)))
            .AddDbRepositories() // Add repositories using the extension method
            .AddScoped<IEmailCampaignProcessingService, EmailCampaignProcessingService>()
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
            .AddSingleton<IConfiguration>(config) // Pass configuration
            .BuildServiceProvider();

        // Resolve the EmailCampaignProcessingService
        var emailCampaignProcessingService = serviceProvider.GetRequiredService<IEmailCampaignProcessingService>();

        // Run the service to process campaigns
        try
        {
            Console.WriteLine("Starting email campaign processing...");
            await emailCampaignProcessingService.ProcessCampaignsAsync();
            Console.WriteLine("Email campaign processing completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during email campaign processing: {ex.Message}");
        }
    }
}