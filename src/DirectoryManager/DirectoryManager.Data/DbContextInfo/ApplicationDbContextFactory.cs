using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DirectoryManager.Data.DbContextInfo
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public IConfigurationRoot? Configuration { get; set; }

        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Provider + connection come from config/environment (never hard-coded). For
            // design-time Postgres commands set DatabaseProvider=Postgres and supply the
            // connection via config or an env var such as ConnectionStrings__PostgresConnection.
            this.Configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile(Common.Constants.StringConstants.AppSettingsFileName, optional: true)
                        .AddEnvironmentVariables()
                        .Build();

            DbProvider.Configure(builder, this.Configuration);

            return new ApplicationDbContext(builder.Options);
        }
    }
}
