using Azure.Storage.Blobs;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.FileStorage.Repositories.Implementations;
using DirectoryManager.FileStorage.Repositories.Interfaces;
using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NowPayments.API.Implementations;
using NowPayments.API.Interfaces;
using NowPayments.API.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logger = new LoggerConfiguration()
   .ReadFrom.Configuration(builder.Configuration)
   .Enrich.FromLogContext()
   .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddResponseCaching();
builder.Services.AddControllersWithViews(); // Add MVC services to the DI container
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddMvc();

// database context
builder.Services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

// database repositories
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
builder.Services.AddScoped<IDirectoryEntryRepository, DirectoryEntryRepository>();
builder.Services.AddScoped<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>();
builder.Services.AddScoped<IDirectoryEntrySelectionRepository, DirectoryEntrySelectionRepository>();
builder.Services.AddScoped<ITrafficLogRepository, TrafficLogRepository>();
builder.Services.AddScoped<IExcludeUserAgentRepository, ExcludeUserAgentRepository>();
builder.Services.AddScoped<ISponsoredListingInvoiceRepository, SponsoredListingInvoiceRepository>();
builder.Services.AddScoped<ISponsoredListingRepository, SponsoredListingRepository>();
builder.Services.AddScoped<IContentSnippetRepository, ContentSnippetRepository>();
builder.Services.AddScoped<ISponsoredListingOfferRepository, SponsoredListingOfferRepository>();
builder.Services.AddScoped<IProcessorConfigRepository, ProcessorConfigRepository>();

// services
builder.Services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();
builder.Services.AddTransient<ICacheService, CacheService>();
builder.Services.AddSingleton<ISiteFilesRepository, SiteFilesRepository>();

builder.Services.AddScoped(provider =>
{
    var configRepo = provider.GetRequiredService<IProcessorConfigRepository>();
    var processorConfigTask = configRepo.GetByProcessorAsync(PaymentProcessor.NOWPayments);
    processorConfigTask.Wait();
    var processorConfig = processorConfigTask.Result;

    if (processorConfig == null)
    {
        throw new Exception("NOWPayments processor config not found");
    }

    var nowPaymentsConfig = JsonConvert.DeserializeObject<NowPaymentConfigs>(processorConfig.Configuration);

    return nowPaymentsConfig == null ?
        throw new Exception("NOWPayments config not found") :
        (INowPaymentsService)new NowPaymentsService(nowPaymentsConfig);
});

builder.Services.AddSingleton<IBlobService>(provider =>
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

builder.Services.AddSingleton<IBlobService>(provider =>
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

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var app = builder.Build();
app.UseResponseCaching();

var userAgentService = app.Services.GetService<UserAgentCacheService>();

var options = new RewriteOptions()
    .AddRedirectToHttpsPermanent()
    .Add(new RedirectWwwToNonWwwRule());

app.UseRewriter(options);

// Configure middleware in the HTTP request pipeline.
app.UseStaticFiles(); // Use static files

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute(); // Map default controller route (usually to HomeController's Index action)

app.Run();