using Azure.Storage.Blobs;
using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Extensions;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.FileStorage.Repositories.Implementations;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Services.Implementations;
using DirectoryManager.Services.Interfaces;
using DirectoryManager.Services.Models;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
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

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    config.GetConnectionString(StringConstants.DefaultConnection),
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }));

            // Repos
            services.AddDbRepositories();

            // Captcha + Http
            services.Configure<CaptchaOptions>(config.GetSection("Captcha"));
            services.AddHttpClient();
            services.AddTransient<ICaptchaService, CaptchaService>();

            // 🔧 Lifetimes: CacheService should be Scoped (it uses a repo/DbContext)
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddScoped<IUrlResolutionService, UrlResolutionService>();
            services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();
            services.AddScoped<ICacheService, CacheService>();           // was Transient → Scoped
            services.AddTransient<IPgpService, PgpService>();
            services.AddSingleton<ISiteFilesRepository, SiteFilesRepository>();
            services.AddScoped<IRssFeedService, RssFeedService>();
            services.AddScoped<IDirectoryEntriesAuditService, DirectoryEntriesAuditService>();

            // ✅ EmailService: sync DI factory, block on async at the edge
            services.AddScoped<IEmailService>(provider =>
            {
                var cacheService = provider.GetRequiredService<ICacheService>();

                var emailConfig = new SendGridConfig
                {
                    ApiKey = cacheService.GetSnippetAsync(SiteConfigSetting.SendGridApiKey).GetAwaiter().GetResult(),
                    SenderEmail = cacheService.GetSnippetAsync(SiteConfigSetting.SendGridSenderEmail).GetAwaiter().GetResult(),
                    SenderName = cacheService.GetSnippetAsync(SiteConfigSetting.SendGridSenderName).GetAwaiter().GetResult(),
                };

                var emailSettings = new EmailSettings
                {
                    UnsubscribeUrlFormat = cacheService.GetSnippetAsync(SiteConfigSetting.EmailSettingUnsubscribeUrlFormat).GetAwaiter().GetResult(),
                    UnsubscribeEmail = cacheService.GetSnippetAsync(SiteConfigSetting.EmailSettingUnsubscribeEmail).GetAwaiter().GetResult(),
                };

                return new EmailService(emailConfig, emailSettings);
            });

            services.AddScoped<IAffiliateAccountRepository, AffiliateAccountRepository>();
            services.AddScoped<IAffiliateCommissionRepository, AffiliateCommissionRepository>();

            // ✅ NOWPayments: avoid .Wait() (AggregateException). Use GetAwaiter().GetResult().
            services.AddScoped<INowPaymentsService>(provider =>
            {
                var configRepo = provider.GetRequiredService<IProcessorConfigRepository>();
                var processorConfig = configRepo.GetByProcessorAsync(PaymentProcessor.NOWPayments)
                                                .GetAwaiter().GetResult()
                                        ?? throw new Exception("NOWPayments processor config not found");

                var nowPaymentsConfig = JsonConvert.DeserializeObject<NowPaymentConfigs>(processorConfig.Configuration)
                                        ?? throw new Exception("NOWPayments config not found");

                return new NowPaymentsService(nowPaymentsConfig);
            });

            services.AddScoped<ISearchBlacklistRepository, SearchBlacklistRepository>();
            services.AddScoped<IChurnService, ChurnService>();

            // ✅ BlobService singleton with a short-lived scope to access scoped services
            services.AddSingleton<IBlobService>(provider =>
            {
                using var scope = provider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                var azureStorageConnection =
                    cacheService.GetSnippetAsync(SiteConfigSetting.AzureStorageConnectionString)
                                .GetAwaiter().GetResult();

                var blobServiceClient = new BlobServiceClient(azureStorageConnection);

                // If CreateAsync truly must run before use, block here once.
                return BlobService.CreateAsync(blobServiceClient).GetAwaiter().GetResult();
            });

            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            return services;
        }
    }
}
