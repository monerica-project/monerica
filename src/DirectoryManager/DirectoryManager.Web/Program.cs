using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResponseCaching();

builder.Services.AddControllersWithViews(); // Add MVC services to the DI container
builder.Services.AddRazorPages();

builder.Services.AddMemoryCache();
builder.Services.AddMvc();

builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
builder.Services.AddScoped<IDirectoryEntryRepository, DirectoryEntryRepository>();
builder.Services.AddScoped<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>();
builder.Services.AddScoped<ITrafficLogRepository, TrafficLogRepository>();
builder.Services.AddScoped<IExcludeUserAgentRepository, ExcludeUserAgentRepository>();

builder.Services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

builder.Services.AddSingleton<UserAgentCacheService>();

builder.Services.AddControllersWithViews();

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.WebHost.UseKestrel(option => option.AddServerHeader = false);

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