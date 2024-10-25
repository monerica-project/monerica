using DirectoryManager.ScheduledNotifier.Services;
using Hangfire;
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
            })
            .Build();

        // Schedule the recurring job using the correct method
        RecurringJob.AddOrUpdate<AdAvailabilityChecker>(
            "check-notifications",
            checker => checker.ScheduleCheckAndSendNotifications(),
            Cron.Hourly); // Runs every hour

        await host.RunAsync();
    }
}
