using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.SponsoredListings;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.DbContextInfo
{
    public interface IApplicationDbContext : IDisposable
    {
        public DbSet<ApplicationUser> ApplicationUser { get; set; }
        public DbSet<ApplicationUserRole> ApplicationUserRole { get; set; }
        public DbSet<BlockedIP> BlockedIPs { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ContentSnippet> ContentSnippets { get; set; }
        public DbSet<DirectoryEntriesAudit> DirectoryEntriesAudit { get; set; }
        public DbSet<DirectoryEntry> DirectoryEntries { get; set; }
        public DbSet<DirectoryEntrySelection> DirectoryEntrySelections { get; set; }
        public DbSet<EmailCampaign> EmailCampaigns { get; set; }
        public DbSet<EmailCampaignMessage> EmailCampaignMessages { get; set; }
        public DbSet<EmailCampaignSubscription> EmailCampaignSubscriptions { get; set; }
        public DbSet<EmailMessage> EmailMessages { get; set; }
        public DbSet<EmailSubscription> EmailSubscriptions { get; set; }
        public DbSet<ExcludeUserAgent> ExcludeUserAgents { get; set; }
        public DbSet<LogEntry> LogEntries { get; set; }
        public DbSet<ProcessorConfig> ProcessorConfigs { get; set; }
        public DbSet<SentEmailRecord> SentEmailRecords { get; set; }
        public DbSet<SponsoredListingOpeningNotification> SponsoredListingOpeningNotifications { get; set; }
        public DbSet<SponsoredListing> SponsoredListings { get; set; }
        public DbSet<SponsoredListingInvoice> SponsoredListingInvoices { get; set; }
        public DbSet<SponsoredListingOffer> SponsoredListingOffers { get; set; }
        public DbSet<SponsoredListingReservation> SponsoredListingReservations { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Subcategory> SubCategories { get; set; }
        public DbSet<TrafficLog> TrafficLogs { get; set; }

        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}