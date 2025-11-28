using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Middleware;
using DirectoryManager.Web.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Rewrite;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load application settings with TorPort fallback if missing
var appSettings = builder.Configuration.GetSection("ApplicationSettings").Get<ApplicationSettings>()
        ?? new ApplicationSettings { TorPort = IntegerConstants.DefaultAlternativePort };

// Configure Serilog logging
var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.Configure<AppSettings>(
    builder.Configuration
           .GetSection("Logging:TrafficLogging"));

// Configure application services
builder.Services.AddApplicationServices(builder.Configuration);

// Configure Kestrel based on environment and certificate availability
builder.WebHost.ConfigureKestrel(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Development ports for Visual Studio debugging with HTTPS
        options.ListenLocalhost(IntegerConstants.DefaultDebuggingHttpPort); // HTTP
        options.ListenLocalhost(IntegerConstants.DefaultDebuggingHttpsPort, listenOptions => listenOptions.UseHttps()); // HTTPS
    }
    else
    {
        // Check if a certificate path is configured for production
        if (builder.Configuration.GetValue<string>("Kestrel:Certificates:Default:Path") != null)
        {
            options.ListenAnyIP(IntegerConstants.DefaultRemoteHttpPort); // HTTP for redirection purposes
            options.ListenAnyIP(IntegerConstants.DefaultRemoteHttpsPort, listenOptions => listenOptions.UseHttps()); // HTTPS
        }
        else
        {
            // No certificate available
            options.ListenAnyIP(IntegerConstants.DefaultRemoteHttpPort); // HTTP
            options.ListenAnyIP(appSettings.TorPort); // HTTP for Tor
        }
    }
});

var app = builder.Build();

// Middleware for HTTPS redirection
app.Use(async (context, next) =>
{
    // Redirect HTTP requests
    if (context.Connection.LocalPort == IntegerConstants.DefaultRemoteHttpPort && !context.Request.IsHttps)
    {
        var httpsUrl = $"https://{context.Request.Host.Host}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(httpsUrl, permanent: true);
        return;
    }

    // Allow requests on TorPort to remain HTTP-only
    await next();
});

// Exception handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            if (exception != null)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(exception, StringConstants.GenericExceptionMessage);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(StringConstants.GenericExceptionMessage);
            }
        });
    });
}

// Additional Middleware and Route Configurations
app.UseResponseCaching();
app.UseStaticFiles();

// Apply ETag only to static paths
app.UseWhen(
    ctx =>
       ctx.Request.Path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
       ctx.Request.Path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase) ||
       ctx.Request.Path.StartsWithSegments("/img", StringComparison.OrdinalIgnoreCase) ||
       ctx.Request.Path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase) ||
       ctx.Request.Path.StartsWithSegments("/fonts", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseMiddleware<ETagMiddleware>());

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (OperationCanceledException) { }
    catch (IOException) { }
});

// Configure URL rewriting for Production
if (!app.Environment.IsDevelopment())
{
    var rewriteOptions = new RewriteOptions().Add(new RedirectWwwToNonWwwRule());
    app.UseRewriter(rewriteOptions);
}

app.UseSession();
app.UseStatusCodePagesWithRedirects("/errors/{0}");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();