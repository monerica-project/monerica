using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Models;
using DirectoryManager.Web.Providers;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using NowPayments.API.Implementations;
using NowPayments.API.Interfaces;
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

// database context
builder.Services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

// services
builder.Services.AddSingleton<IUserAgentCacheService, UserAgentCacheService>();

builder.Services.AddScoped<INowPaymentsService>(x =>
{
    var configProvider = new NowPaymentsConfigProvider(builder.Configuration);
    var configs = configProvider.GetConfigs();
    return new NowPaymentsService(configs);
});

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var appSettingsSponsoredListingOffers = builder.Configuration.GetSection("SponsoredListingOffers");
var sponsoredListings = new SponsoredListingOffersContainer();
appSettingsSponsoredListingOffers.Bind(sponsoredListings.SponsoredListingOffers);
builder.Services.AddSingleton(sponsoredListings);

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