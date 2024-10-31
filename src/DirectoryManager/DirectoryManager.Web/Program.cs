using DirectoryManager.Web.AppRules;
using DirectoryManager.Web.Extensions;
using DirectoryManager.Web.Middleware;
using DirectoryManager.Web.Services.Implementations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Rewrite;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog logging
var logger = new LoggerConfiguration()
   .ReadFrom.Configuration(builder.Configuration)
   .Enrich.FromLogContext()
   .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

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
            if (exceptionHandlerPathFeature != null)
            {
                var exception = exceptionHandlerPathFeature.Error;

                var logger = app.Services.GetRequiredService<ILogger<Program>>();

                // Log the exception
                logger.LogError(exception, "An unhandled exception occurred.");

                // Optionally, you can add custom response handling here
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An unexpected error happened.");
            }
        });
    });
}

app.UseResponseCaching();

var userAgentService = app.Services.GetService<UserAgentCacheService>();

var options = new RewriteOptions()
    .AddRedirectToHttpsPermanent()
    .Add(new RedirectWwwToNonWwwRule());

app.UseRewriter(options);

// Configure middleware in the HTTP request pipeline.
app.UseStaticFiles(); // Use static files

app.UseMiddleware<ETagMiddleware>();

app.UseStatusCodePagesWithRedirects("/errors/{0}");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute(); // Map default controller route (usually to HomeController's Index action)

app.Run();