using Azure.Storage.Blobs;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.FileStorage.Repositories.Implementations;
using DirectoryManager.FileStorage.Repositories.Interfaces;
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
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddResponseCaching();
            services.AddControllersWithViews();
            services.AddRazorPages();
            services.AddMemoryCache();
            services.AddMvc();
            services.AddHttpContextAccessor();

            // Register ApplicationDbContext with DbContextOptions
            services.AddDbContext<ApplicationDbContext>(options =>
                  options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Register ApplicationDbContext as IApplicationDbContext
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

            // Register ApplicationDbContext as IApplicationDbContext
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

            // Database repositories
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ISubcategoryRepository, SubcategoryRepository>();
            services.AddScoped<IDirectoryEntryRepository, DirectoryEntryRepository>();
            services.AddScoped<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>();
            services.AddScoped<IDirectoryEntrySelectionRepository, DirectoryEntrySelectionRepository>();
            services.AddScoped<ITrafficLogRepository, TrafficLogRepository>();
            services.AddScoped<IExcludeUserAgentRepository, ExcludeUserAgentRepository>();
            services.AddScoped<ISponsoredListingInvoiceRepository, SponsoredListingInvoiceRepository>();
            services.AddScoped<ISponsoredListingRepository, SponsoredListingRepository>();
            services.AddScoped<ISponsoredListingReservationRepository, SponsoredListingReservationRepository>();
            services.AddScoped<ISponsoredListingOfferRepository, SponsoredListingOfferRepository>();
            services.AddScoped<IContentSnippetRepository, ContentSnippetRepository>();
            services.AddScoped<IProcessorConfigRepository, ProcessorConfigRepository>();
            services.AddScoped<IEmailSubscriptionRepository, EmailSubscriptionRepository>();
            services.AddScoped<IBlockedIPRepository, BlockedIPRepository>();

            // Services
            services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();
            services.AddTransient<ICacheService, CacheService>();
            services.AddSingleton<ISiteFilesRepository, SiteFilesRepository>();
            services.AddScoped<IRssFeedService, RssFeedService>();

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