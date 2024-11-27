using DirectoryManager.ScheduledNotifier.Services.Implementations;
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
      //  var host = Host.CreateDefaultBuilder(args)
      //      .ConfigureServices((hostContext, services) =>
      //      {
      //          // Configure Hangfire with SQL Server
      //          services.AddHangfire(config =>
      //              config.UseSqlServerStorage("your-connection-string"));

      ////          services.AddDbContext<ApplicationDbContext>(options =>
      ////options.UseSqlServer(configuration.GetConnectionString(StringConstants.DefaultConnection)));


      //          services.AddHangfireServer();
 

      //          // Register HttpClient
      //          services.AddHttpClient();

      //          // Configure and register SendGrid email service
      //          var sendGridConfig = hostContext.Configuration.GetSection("SendGrid").Get<SendGridConfig>();
      //          services.AddSingleton(sendGridConfig);
      //          services.AddTransient<IEmailService, EmailService>();
      //      })
      //      .Build();

      //  // Schedule the recurring job using the correct method
      //  //RecurringJob.AddOrUpdate<AdAvailabilityChecker>(
      //  //    "check-notifications",
      //  //    checker => checker.ScheduleCheckAndSendNotifications(),
      //  //    Cron.Hourly); // Runs every hour


      //  var emailService = host.Services.GetRequiredService<IEmailService>();
      //  await emailService.SendEmailAsync("test456", "test message", "<p>test html message</p>", new List<string>() { "" });

      //  await host.RunAsync();
    }
}
