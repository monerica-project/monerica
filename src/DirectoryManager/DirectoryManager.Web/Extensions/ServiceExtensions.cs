using Azure.Storage.Blobs;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.FileStorage.Repositories.Implementations;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NowPayments.API.Implementations;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;

namespace DirectoryManager.Web.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration config)
        {
            services.AddSession();
            services.AddResponseCaching();
            services.AddControllersWithViews();
            services.AddRazorPages();
            services.AddMemoryCache();
            services.AddMvc();
            services.AddHttpContextAccessor();

            // Register ApplicationDbContext with DbContextOptions
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                  config.GetConnectionString(StringConstants.DefaultConnection),
                  sqlOptions =>
                  {
                        // retry up to 5 times with up to 10s between retries
                        sqlOptions.EnableRetryOnFailure(
                          maxRetryCount: 5,
                          maxRetryDelay: TimeSpan.FromSeconds(30),
                          errorNumbersToAdd: null);
                  }));

            // Register all repositories from DatabaseExtensions
            services.AddDbRepositories();

            // Services
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddScoped<IUrlResolutionService, UrlResolutionService>();
            services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();
            services.AddTransient<ICacheService, CacheService>();
            services.AddSingleton<ISiteFilesRepository, SiteFilesRepository>();
            services.AddScoped<IRssFeedService, RssFeedService>();
            services.AddScoped<IDirectoryEntriesAuditService, DirectoryEntriesAuditService>();
            services.AddScoped<IEmailService, EmailService>(provider =>
              {
                  using var scope = provider.CreateScope();
                  var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                  var emailConfig = new SendGridConfig
                  {
                      ApiKey = cacheService.GetSnippet(SiteConfigSetting.SendGridApiKey),
                      SenderEmail = cacheService.GetSnippet(SiteConfigSetting.SendGridSenderEmail),
                      SenderName = cacheService.GetSnippet(SiteConfigSetting.SendGridSenderName)
                  };

                  var emailSettings = new EmailSettings
                  {
                      UnsubscribeUrlFormat = cacheService.GetSnippet(SiteConfigSetting.EmailSettingUnsubscribeUrlFormat),
                      UnsubscribeEmail = cacheService.GetSnippet(SiteConfigSetting.EmailSettingUnsubscribeEmail),
                  };

                  return new EmailService(emailConfig, emailSettings);
              });

            // NOWPayments configuration and service registration
            services.AddScoped<INowPaymentsService>(provider =>
            {
                var configRepo = provider.GetRequiredService<IProcessorConfigRepository>();
                var processorConfigTask = configRepo.GetByProcessorAsync(PaymentProcessor.NOWPayments);
                processorConfigTask.Wait();
                var processorConfig = processorConfigTask.Result ?? throw new Exception("NOWPayments processor config not found");

                var nowPaymentsConfig = JsonConvert.DeserializeObject<NowPaymentConfigs>(processorConfig.Configuration)
                                    ?? throw new Exception("NOWPayments config not found");

                return new NowPaymentsService(nowPaymentsConfig);
            });

            // BlobService with Azure Storage configuration
            services.AddSingleton<IBlobService>(provider =>
            {
                return Task.Run(async () =>
                {
                    using var scope = provider.CreateScope();
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                    var azureStorageConnection = cacheService.GetSnippet(SiteConfigSetting.AzureStorageConnectionString);
                    var blobServiceClient = new BlobServiceClient(azureStorageConnection);

                    return await BlobService.CreateAsync(blobServiceClient);
                }).GetAwaiter().GetResult();
            });

            // Route options configuration
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            // Identity configuration
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            return services;
        }
    }
}
