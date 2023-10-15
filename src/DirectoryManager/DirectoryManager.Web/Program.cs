using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Services.Implementations;
using DirectoryManager.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using NowPayments.API.Implementations;
using NowPayments.API.Interfaces;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IPaymentService>(x =>
{
    var apiKey = builder.Configuration.GetSection("NowPayments:ApiKey")?.Value;

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new ArgumentNullException("NowPaymentsApiKey", "The API key for NowPayments is not configured or is empty.");
    }

    return new PaymentService(apiKey);
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