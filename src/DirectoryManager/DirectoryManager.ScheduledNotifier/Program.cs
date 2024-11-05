using DirectoryManager.ScheduledNotifier.Services;
using DirectoryManager.Services.Models;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // Configure Hangfire with SQL Server
                services.AddHangfire(config =>
                    config.UseSqlServerStorage("your-connection-string"));

                services.AddHangfireServer();

                // Register services
                services.AddSingleton<NotificationService>();
                services.AddSingleton<AdAvailabilityChecker>();

                // Register HttpClient
                services.AddHttpClient();

                // Configure and register SendGrid email service
                var sendGridConfig = hostContext.Configuration.GetSection("SendGrid").Get<SendGridConfig>();
                services.AddSingleton(sendGridConfig);
                services.AddTransient<IEmailService, EmailService>();
            })
            .Build();

        // Schedule the recurring job using the correct method
        //RecurringJob.AddOrUpdate<AdAvailabilityChecker>(
        //    "check-notifications",
        //    checker => checker.ScheduleCheckAndSendNotifications(),
        //    Cron.Hourly); // Runs every hour


        var emailService = host.Services.GetRequiredService<IEmailService>();
        await emailService.SendEmailAsync("test456", "test message", "<p>test html message</p>", new List<string>() { "admin@bootbaron.com" });

        await host.RunAsync();
    }
}
