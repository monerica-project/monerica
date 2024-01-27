using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.SponsoredListings;
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

        public DbSet<LogEntry> LogEntries { get; set; }

        public DbSet<ContentSnippet> ContentSnippets { get; set; }

        public DbSet<SponsoredListingOffer> SponsoredListingOffers { get; set; }

        public DbSet<ProcessorConfig> ProcessorConfigs { get; set; }

        public DbSet<SponsoredListingReservation> SponsoredListingReservations { get; set; }

        int SaveChanges();

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}