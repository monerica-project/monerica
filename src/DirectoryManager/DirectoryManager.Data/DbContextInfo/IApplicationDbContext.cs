using DirectoryManager.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.DbContextInfo
{
    public interface IApplicationDbContext : IDisposable
    {
        public DbSet<Category> Categories { get; set; }

        public DbSet<SubCategory> SubCategories { get; set; }

        public DbSet<DirectoryEntry> DirectoryEntries { get; set; }

        public DbSet<Submission> Submissions { get; set; }

        public DbSet<ApplicationUser> ApplicationUser { get; set; }

        public DbSet<ApplicationUserRole> ApplicationUserRole { get; set; }

        public DbSet<DirectoryEntriesAudit> DirectoryEntriesAudit { get; set; }

        public DbSet<TrafficLog> TrafficLogs { get; set; }

        public DbSet<ExcludeUserAgent> ExcludeUserAgents { get; set; }

        public DbSet<DirectoryEntrySelection> DirectoryEntrySelections { get; set; }

        public DbSet<SponsoredListing> SponsoredListings { get; set; }

        public DbSet<SponsoredListingInvoice> SponsoredListingInvoices { get; set; }

        int SaveChanges();

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}