using Microsoft.EntityFrameworkCore;
using System.Reflection;
using DirectoryManager.Data.Models;
using DirectoryManager.Data.Models.Affiliates;
using DirectoryManager.Data.Models.BaseModels;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.Reviews;
using DirectoryManager.Data.Models.SponsoredListings;

namespace DirectoryManager.Data.DbContextInfo
{
    public class ApplicationDbContext : ApplicationBaseContext<ApplicationDbContext>, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

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
        public DbSet<Subcategory> Subcategories { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<TrafficLog> TrafficLogs { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<DirectoryEntryTag> DirectoryEntryTags { get; set; }
        public DbSet<SearchLog> SearchLogs { get; set; }
        public DbSet<ReviewerKey> ReviewerKeys { get; set; }
        public DbSet<DirectoryEntryReview> DirectoryEntryReviews { get; set; }
        public DbSet<DirectoryEntryReviewComment> DirectoryEntryReviewComments { get; set; } = null!;
        public DbSet<AffiliateAccount> AffiliateAccounts { get; set; }
        public DbSet<AffiliateCommission> AffiliateCommissions { get; set; }
        public DbSet<SearchBlacklistTerm> SearchBlacklistTerms { get; set; }
        public DbSet<AdditionalLink> AdditionalLinks { get; set; }
        public DbSet<ReviewTag> ReviewTags { get; set; }
        public DbSet<DirectoryEntryReviewTag> DirectoryEntryReviewTags { get; set; }


        public override int SaveChanges()
        {
            this.SetDates();

            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            this.SetDates();

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            ConfigureIndexes(builder);                // ✅ ALL HasIndex() calls
            ConfigureKeysAndRelationships(builder);   // ✅ keys + relationships only (no HasIndex)
            ConfigurePropertyMappings(builder);       // ✅ column types, max lengths, table names, etc. (no HasIndex)
        }

        // =========================================================
        // INDEXES (ALL HasIndex() CALLS LIVE HERE)
        // =========================================================
        private static void ConfigureIndexes(ModelBuilder builder)
        {
            ConfigureSponsoredListingOpeningNotificationIndexes(builder);
            ConfigureSponsoredListingIndexes(builder);
            ConfigureDirectoryEntryIndexes(builder);
            ConfigureTagCategorySubcategoryIndexes(builder);

            ConfigureTrafficAndUserAgentIndexes(builder);
            ConfigureSponsoredListingInvoiceIndexes(builder);
            ConfigureSponsoredListingOfferIndexes(builder);
            ConfigureSponsoredListingReservationIndexes(builder);

            ConfigureProcessorConfigIndexes(builder);
            ConfigureEmailIndexes(builder);
            ConfigureBlockedIpIndexes(builder);

            ConfigureAffiliateIndexes(builder);
            ConfigureReviewerAndReviewIndexes(builder);
            ConfigureAdditionalLinkIndexes(builder);

            ConfigureSearchBlacklistAndReviewTagIndexes(builder);
        }

        private static void ConfigureSponsoredListingOpeningNotificationIndexes(ModelBuilder builder)
        {
            builder.Entity<SponsoredListingOpeningNotification>()
                   .HasIndex(x => new { x.Email, x.SponsorshipType, x.TypeId, x.DirectoryEntryId });

            builder.Entity<SponsoredListingOpeningNotification>()
                   .HasIndex(x => new { x.SponsorshipType, x.TypeId, x.IsActive, x.IsReminderSent, x.SubscribedDate });

            builder.Entity<SponsoredListingOpeningNotification>()
                   .HasIndex(e => new { e.Email, e.SponsorshipType, e.TypeId, e.SubscribedDate })
                   .IsUnique()
                   .HasDatabaseName("IX_SponsoredListingOpeningNotification_Unique");

            // For GetSubscribers():
            // WHERE IsActive = 1 AND IsReminderSent = 0
            // ORDER BY SubscribedDate, SponsoredListingOpeningNotificationId
            builder.Entity<SponsoredListingOpeningNotification>()
                   .HasIndex(x => new { x.SubscribedDate, x.SponsoredListingOpeningNotificationId })
                   .HasDatabaseName("IX_SponsoredListingOpeningNotification_Queue")
                   .HasFilter("[IsActive] = 1 AND [IsReminderSent] = 0")
                   // EF Core 10 + SqlServer supports INCLUDE columns
                   .IncludeProperties(x => new
                   {
                       x.Email,
                       x.SponsorshipType,
                       x.TypeId,
                       x.DirectoryEntryId
                   });
        }

        private static void ConfigureSponsoredListingIndexes(ModelBuilder builder)
        {
            builder.Entity<SponsoredListing>()
                   .HasIndex(e => new { e.CreateDate, e.UpdateDate });

            builder.Entity<SponsoredListing>()
                   .HasIndex(e => e.CampaignEndDate);

            // For GetActiveSponsorsByTypeAsync(type):
            // WHERE SponsorshipType = @type
            //   AND CampaignStartDate <= @now
            //   AND CampaignEndDate   >= @now
            // ORDER BY CampaignEndDate DESC, CampaignStartDate DESC
            builder.Entity<SponsoredListing>()
                   .HasIndex(x => new { x.SponsorshipType, x.CampaignEndDate, x.CampaignStartDate })
                   .HasDatabaseName("IX_SponsoredListings_Type_End_Start")
                   // EF Core 10 supports descending per-column
                   .IsDescending(false, true, true)
                   .IncludeProperties(x => new { x.DirectoryEntryId });

            // For GetAllActiveSponsorsAsync():
            // WHERE CampaignStartDate <= @now AND CampaignEndDate >= @now
            // ORDER BY CampaignEndDate DESC, CampaignStartDate DESC
            builder.Entity<SponsoredListing>()
                   .HasIndex(x => new { x.CampaignEndDate, x.CampaignStartDate })
                   .HasDatabaseName("IX_SponsoredListings_End_Start")
                   .IsDescending(true, true)
                   .IncludeProperties(x => new { x.DirectoryEntryId, x.SponsorshipType });

            // Helpful for includes/joins (often created automatically, but explicit is fine)
            builder.Entity<SponsoredListing>()
                   .HasIndex(x => x.DirectoryEntryId)
                   .HasDatabaseName("IX_SponsoredListings_DirectoryEntryId");
        }

        private static void ConfigureDirectoryEntryIndexes(ModelBuilder builder)
        {
            builder.Entity<DirectoryEntry>()
                   .HasIndex(e => new { e.DirectoryEntryId, e.DirectoryStatus })
                   .IsUnique();

            builder.Entity<DirectoryEntry>()
                   .HasIndex(e => e.Link)
                   .IsUnique();

            builder.Entity<DirectoryEntry>()
                   .HasIndex(e => new { e.SubCategoryId, e.DirectoryEntryKey })
                   .IsUnique();

            builder.Entity<DirectoryEntry>()
                   .HasIndex(e => e.DirectoryEntryKey)
                   .IsUnique();

            builder.Entity<DirectoryEntry>()
                   .HasIndex(e => e.SubCategoryId)
                   .HasDatabaseName("IX_DirectoryEntries_SubCategoryId");
        }

        private static void ConfigureTagCategorySubcategoryIndexes(ModelBuilder builder)
        {
            builder.Entity<Tag>()
                   .HasIndex(t => t.Name)
                   .IsUnique();

            builder.Entity<Tag>()
                   .HasIndex(t => t.Key)
                   .IsUnique();

            builder.Entity<Category>()
                   .HasIndex(e => e.CategoryKey)
                   .IsUnique();

            builder.Entity<Subcategory>()
                   .HasIndex(e => new { e.SubCategoryKey, e.CategoryId })
                   .IsUnique();

            // Helps Include chain: DirectoryEntry -> SubCategory -> Category
            builder.Entity<Subcategory>()
                   .HasIndex(e => e.CategoryId)
                   .HasDatabaseName("IX_Subcategories_CategoryId");
        }

        private static void ConfigureTrafficAndUserAgentIndexes(ModelBuilder builder)
        {
            builder.Entity<TrafficLog>()
                   .HasIndex(t => t.CreateDate)
                   .HasDatabaseName("IX_TrafficLog_CreateDate");

            builder.Entity<ExcludeUserAgent>()
                   .HasIndex(e => e.UserAgent)
                   .IsUnique();
        }

        private static void ConfigureSponsoredListingInvoiceIndexes(ModelBuilder builder)
        {
            builder.Entity<SponsoredListingInvoice>()
                   .HasIndex(e => e.InvoiceId)
                   .IsUnique();

            builder.Entity<SponsoredListingInvoice>()
                   .HasIndex(i => new { i.DirectoryEntryId, i.PaymentStatus })
                   .HasDatabaseName("IX_Invoice_Dir_PaidStatus");
        }

        private static void ConfigureSponsoredListingOfferIndexes(ModelBuilder builder)
        {
            builder.Entity<SponsoredListingOffer>()
                   .HasIndex(e => new { e.SponsorshipType, e.Days, e.CategoryId, e.SubcategoryId })
                   .IsUnique();

            builder.Entity<SponsoredListingOffer>(eb =>
            {
                eb.HasIndex(e => new { e.SponsorshipType, e.Days, e.CategoryId })
                  .IsUnique()
                  .HasFilter("[SubcategoryId] IS NULL")
                  .HasDatabaseName("UX_Offer_Type_Days_Cat_NoSubcat");

                eb.HasIndex(e => new { e.SponsorshipType, e.Days, e.CategoryId, e.SubcategoryId })
                  .IsUnique()
                  .HasFilter("[SubcategoryId] IS NOT NULL")
                  .HasDatabaseName("UX_Offer_Type_Days_Cat_Subcat");
            });

            builder.Entity<SponsoredListingOffer>()
                   .HasIndex(e => new { e.SponsorshipType, e.Days })
                   .IsUnique()
                   .HasFilter("[SubcategoryId] IS NULL");
        }

        private static void ConfigureSponsoredListingReservationIndexes(ModelBuilder builder)
        {
            builder.Entity<SponsoredListingReservation>()
                   .HasIndex(e => e.ReservationGuid)
                   .IsUnique();

            builder.Entity<SponsoredListingReservation>()
                   .HasIndex(e => e.ExpirationDateTime);
        }

        private static void ConfigureProcessorConfigIndexes(ModelBuilder builder)
        {
            builder.Entity<ProcessorConfig>()
                   .HasIndex(e => e.PaymentProcessor)
                   .IsUnique();
        }

        private static void ConfigureEmailIndexes(ModelBuilder builder)
        {
            builder.Entity<EmailMessage>()
                   .HasIndex(e => e.EmailKey)
                   .IsUnique();

            builder.Entity<SentEmailRecord>()
                   .HasIndex(e => new { e.EmailSubscriptionId, e.EmailMessageId })
                   .IsUnique();

            builder.Entity<EmailSubscription>()
                   .HasIndex(e => e.IsSubscribed);

            builder.Entity<EmailSubscription>()
                   .HasIndex(e => e.Email)
                   .IsUnique();

            builder.Entity<EmailCampaignSubscription>()
                   .HasIndex(e => new { e.EmailCampaignId, e.IsActive });

            builder.Entity<EmailCampaignSubscription>()
                   .HasIndex(e => e.EmailSubscriptionId);

            builder.Entity<EmailCampaign>()
                   .HasIndex(e => e.EmailCampaignKey)
                   .IsUnique();
        }

        private static void ConfigureBlockedIpIndexes(ModelBuilder builder)
        {
            builder.Entity<BlockedIP>()
                   .HasIndex(e => e.IpAddress)
                   .IsUnique();
        }

        private static void ConfigureAffiliateIndexes(ModelBuilder builder)
        {
            builder.Entity<AffiliateAccount>()
                   .HasIndex(x => x.ReferralCode)
                   .IsUnique()
                   .HasDatabaseName("UX_AffiliateAccount_ReferralCode");

            builder.Entity<AffiliateCommission>()
                   .HasIndex(x => x.SponsoredListingInvoiceId)
                   .IsUnique()
                   .HasDatabaseName("UX_AffiliateCommission_Invoice");
        }

        private static void ConfigureReviewerAndReviewIndexes(ModelBuilder builder)
        {
            builder.Entity<ReviewerKey>()
                   .HasIndex(x => x.Fingerprint)
                   .IsUnique();

            builder.Entity<DirectoryEntryReview>(r =>
            {
                r.HasIndex(x => x.DirectoryEntryId);
                r.HasIndex(x => new { x.DirectoryEntryId, x.ModerationStatus });
                r.HasIndex(x => x.AuthorFingerprint);
            });
        }

        private static void ConfigureAdditionalLinkIndexes(ModelBuilder builder)
        {
            builder.Entity<AdditionalLink>()
                   .HasIndex(x => new { x.DirectoryEntryId, x.SortOrder })
                   .IsUnique();
        }

        private static void ConfigureSearchBlacklistAndReviewTagIndexes(ModelBuilder builder)
        {
            builder.Entity<SearchBlacklistTerm>()
                   .HasIndex(e => e.Term)
                   .IsUnique();

            builder.Entity<ReviewTag>()
                   .HasIndex(x => x.Slug)
                   .IsUnique();
        }

        // =========================================================
        // KEYS + RELATIONSHIPS (NO HasIndex HERE)
        // =========================================================
        private static void ConfigureKeysAndRelationships(ModelBuilder builder)
        {
            builder.Entity<DirectoryEntryTag>()
                   .HasKey(et => new { et.DirectoryEntryId, et.TagId });

            builder.Entity<DirectoryEntryTag>()
                   .HasOne(et => et.DirectoryEntry)
                   .WithMany(de => de.EntryTags)
                   .HasForeignKey(et => et.DirectoryEntryId);

            builder.Entity<DirectoryEntryTag>()
                   .HasOne(et => et.Tag)
                   .WithMany(t => t.EntryTags)
                   .HasForeignKey(et => et.TagId);

            builder.Entity<SponsoredListing>()
                   .HasOne(sl => sl.DirectoryEntry)
                   .WithMany()
                   .HasForeignKey(sl => sl.DirectoryEntryId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SponsoredListing>()
                   .HasOne(sl => sl.SponsoredListingInvoice)
                   .WithOne(sli => sli.SponsoredListing)
                   .HasForeignKey<SponsoredListing>(sl => sl.SponsoredListingInvoiceId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AffiliateCommission>(ac =>
            {
                ac.HasOne(x => x.AffiliateAccount)
                  .WithMany(a => a.Commissions)
                  .HasForeignKey(x => x.AffiliateAccountId)
                  .OnDelete(DeleteBehavior.Cascade);

                ac.HasOne(x => x.SponsoredListingInvoice)
                  .WithMany()
                  .HasForeignKey(x => x.SponsoredListingInvoiceId)
                  .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DirectoryEntryReviewComment>()
                   .HasOne(x => x.DirectoryEntryReview)
                   .WithMany(r => r.Comments)
                   .HasForeignKey(x => x.DirectoryEntryReviewId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DirectoryEntryReviewComment>()
                   .HasOne(x => x.ParentComment)
                   .WithMany(x => x.Children)
                   .HasForeignKey(x => x.ParentCommentId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DirectoryEntryReviewTag>()
                   .HasKey(x => new { x.DirectoryEntryReviewId, x.ReviewTagId });

            builder.Entity<DirectoryEntryReviewTag>()
                   .HasOne(x => x.DirectoryEntryReview)
                   .WithMany(r => r.ReviewTags)
                   .HasForeignKey(x => x.DirectoryEntryReviewId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DirectoryEntryReviewTag>()
                   .HasOne(x => x.ReviewTag)
                   .WithMany(t => t.ReviewLinks)
                   .HasForeignKey(x => x.ReviewTagId)
                   .OnDelete(DeleteBehavior.Cascade);
        }

        // =========================================================
        // PROPERTY / COLUMN MAPPINGS (NO HasIndex HERE)
        // =========================================================
        private static void ConfigurePropertyMappings(ModelBuilder builder)
        {
            builder.Entity<SponsoredListingInvoice>()
                   .Property(e => e.Amount)
                   .HasColumnType("decimal(20, 12)");

            builder.Entity<SponsoredListingInvoice>()
                   .Property(e => e.PaidAmount)
                   .HasColumnType("decimal(20, 12)");

            builder.Entity<SponsoredListingInvoice>()
                   .Property(e => e.OutcomeAmount)
                   .HasColumnType("decimal(20, 12)");

            builder.Entity<SponsoredListingOffer>()
                   .Property(e => e.Price)
                   .HasColumnType("decimal(20, 12)");

            builder.Entity<AffiliateCommission>()
                   .Property(x => x.AmountDue)
                   .HasColumnType("decimal(18,8)");

            builder.Entity<AffiliateAccount>(aa =>
            {
                aa.Property(x => x.ReferralCode)
                  .HasMaxLength(12)
                  .IsRequired();

                aa.Property(x => x.WalletAddress)
                  .HasMaxLength(256)
                  .IsRequired();

                aa.Property(x => x.Email)
                  .HasMaxLength(256);
            });

            builder.Entity<ReviewerKey>(rk =>
            {
                rk.ToTable("ReviewerKeys");

                rk.Property(x => x.Fingerprint)
                  .HasMaxLength(64)
                  .IsRequired();

                rk.Property(x => x.PublicKeyBlock)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

                rk.Property(x => x.Alias)
                  .HasMaxLength(64);
            });

            builder.Entity<AdditionalLink>(b =>
            {
                b.Property(x => x.Link).HasMaxLength(500);
            });

            builder.Entity<DirectoryEntryReview>(r =>
            {
                r.ToTable("DirectoryEntryReviews");
                r.Property(x => x.RowVersion).IsRowVersion();
            });
        }

        private void SetDates()
        {
            foreach (var entry in this.ChangeTracker.Entries()
                .Where(x => (x.Entity is StateInfo) && x.State == EntityState.Added)
                .Select(x => (StateInfo)x.Entity))
            {
                if (entry.CreateDate == DateTime.MinValue)
                {
                    entry.CreateDate = DateTime.UtcNow;
                }
            }

            foreach (var entry in this.ChangeTracker.Entries()
                .Where(x => x.Entity is CreatedStateInfo && x.State == EntityState.Added)
                .Select(x => (CreatedStateInfo)x.Entity)
                .Where(x => x != null))
            {
                if (entry.CreateDate == DateTime.MinValue)
                {
                    entry.CreateDate = DateTime.UtcNow;
                }
            }

            foreach (var entry in this.ChangeTracker.Entries()
                .Where(x => x.Entity is StateInfo && x.State == EntityState.Modified)
                .Select(x => (StateInfo)x.Entity)
                .Where(x => x != null))
            {
                entry.UpdateDate = DateTime.UtcNow;
            }
        }

        object IApplicationDbContext.Set<T>()
        {
            var method = typeof(DbContext)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == "Set"
                            && m.IsGenericMethodDefinition
                            && m.GetGenericArguments().Length == 1
                            && m.GetParameters().Length == 0);

            var generic = method.MakeGenericMethod(typeof(T));
            return generic.Invoke(this, null)!;
        }
    }
}