using System;
using DirectoryManager.Data.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.Data.DbContextInfo
{
    /// <summary>
    /// Configures the ApplicationDbContext to use PostgreSQL. The connection string
    /// always comes from configuration (the existing "DefaultConnection" key) — never
    /// hard-coded — and a clear error is thrown if it is missing. Cutover from SQL Server
    /// is just swapping the DefaultConnection value to a PostgreSQL connection string.
    /// </summary>
    public static class DbProvider
    {
        public static void Configure(DbContextOptionsBuilder options, IConfiguration config)
        {
            var connectionString = config.GetConnectionString(StringConstants.DefaultConnection);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured. " +
                    "Set it in configuration (appsettings / environment).");
            }

            options.UseNpgsql(
                connectionString,
                o => o.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null));
        }
    }
}
