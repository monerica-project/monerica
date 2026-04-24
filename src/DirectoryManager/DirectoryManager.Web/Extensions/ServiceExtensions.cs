using Azure.Storage.Blobs;
using BtcPayServer.API.Implementations;
using BtcPayServer.API.Interfaces;
using BtcPayServer.API.Models;
using DirectoryManager.Common.Interfaces;
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

            // Lifetimes: CacheService should be Scoped (it uses a repo/DbContext)
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddScoped<IUrlResolutionService, UrlResolutionService>();
            services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();
            services.AddScoped<ICacheService, CacheService>();

            // Domain registration date lookup (RDAP -> WHOIS fallback)
            services.AddHttpClient<IDomainRegistrationDateService, DomainRegistrationDateService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DirectoryManager/1.0");
            });

            services.AddScoped<ISponsorTickerService, SponsorTickerService>();
            services.AddTransient<IPgpService, PgpService>();
            services.AddSingleton<ISiteFilesRepository, SiteFilesRepository>();
            services.AddScoped<IRssFeedService, RssFeedService>();
            services.AddScoped<IDirectoryEntriesAuditService, DirectoryEntriesAuditService>();

            // EmailService: sync DI factory, block on async at the edge
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

            services.AddScoped<ISearchBlacklistCache, Services.SearchBlacklistCache>();

            // NOWPayments: config loaded from DB via IProcessorConfigRepository
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

            // BtcPayServerService: config loaded from DB via IProcessorConfigRepository.
            // Registered once as the concrete class, then aliased to both interfaces
            // so each scope gets ONE instance serving IBtcPayServerService AND IRateConversionService.
            services.AddScoped<BtcPayServerService>(provider =>
            {
                var configRepo = provider.GetRequiredService<IProcessorConfigRepository>();
                var processorConfig = configRepo.GetByProcessorAsync(PaymentProcessor.BTCPayServer)
                                                .GetAwaiter().GetResult()
                                        ?? throw new Exception("BTCPayServer processor config not found");

                var btcPayConfig = JsonConvert.DeserializeObject<BtcPayServerConfigs>(processorConfig.Configuration)
                                   ?? throw new Exception("BTCPayServer config not found");

                return new BtcPayServerService(btcPayConfig);
            });

            services.AddScoped<IBtcPayServerService>(sp => sp.GetRequiredService<BtcPayServerService>());
            services.AddScoped<IRateConversionService>(sp => sp.GetRequiredService<BtcPayServerService>());

            services.AddScoped<ISearchBlacklistRepository, SearchBlacklistRepository>();
            services.AddScoped<IChurnService, ChurnService>();
            services.AddScoped<IUserContentModerationService, UserContentModerationService>();

            // BlobService singleton with a short-lived scope to access scoped services
            services.AddSingleton<IBlobService>(provider =>
            {
                using var scope = provider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                var azureStorageConnection =
                    cacheService.GetSnippetAsync(SiteConfigSetting.AzureStorageConnectionString)
                                .GetAwaiter().GetResult();

                var blobServiceClient = new BlobServiceClient(azureStorageConnection);

                return BlobService.CreateAsync(blobServiceClient).GetAwaiter().GetResult();
            });

            services.AddHttpClient("OrderProofVerifier", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(6);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MonericaReviewVerifier/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                         System.Net.DecompressionMethods.Deflate |
                                         System.Net.DecompressionMethods.Brotli
            });

            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            return services;
        }
    }
}