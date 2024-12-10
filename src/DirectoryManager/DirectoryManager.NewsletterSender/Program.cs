using DirectoryManager.Common.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.NewsletterSender.Services.Implementations;
using DirectoryManager.NewsletterSender.Services.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Build configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile(StringConstants.AppSettingsFileName, optional: true, reloadOnChange: true)
            .Build();

        // Register services in the service container
        var serviceProvider = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(config.GetConnectionString(DirectoryManager.Data.Constants.StringConstants.DefaultConnection)))
            .AddRepositories() // Add repositories using the extension method
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

                return new EmailService(emailConfig);
            })
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