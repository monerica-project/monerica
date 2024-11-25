﻿using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Repositories.Implementations;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryManager.Data.Extensions
{
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Adds all database repositories to the service collection.
        /// </summary>
        /// <param name="services">IServiceCollection.</param>
        /// <returns>IServiceCollection as extension.</returns>
        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            // Register ApplicationDbContext as IApplicationDbContext
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

            // Sponsorship repositories
            services.AddScoped<ISponsoredListingInvoiceRepository, SponsoredListingInvoiceRepository>();
            services.AddScoped<ISponsoredListingRepository, SponsoredListingRepository>();
            services.AddScoped<ISponsoredListingReservationRepository, SponsoredListingReservationRepository>();
            services.AddScoped<ISponsoredListingOfferRepository, SponsoredListingOfferRepository>();

            // Email repositories
            services.AddScoped<IEmailSubscriptionRepository, EmailSubscriptionRepository>();
            services.AddScoped<IEmailMessageRepository, EmailMessageRepository>();
            services.AddScoped<IEmailCampaignMessageRepository, EmailCampaignMessageRepository>();
            services.AddScoped<IEmailCampaignRepository, EmailCampaignRepository>();
            services.AddScoped<IEmailCampaignSubscriptionRepository, EmailCampaignSubscriptionRepository>();

            // Other repositories
            services.AddScoped<ISubmissionRepository, SubmissionRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ISubcategoryRepository, SubcategoryRepository>();
            services.AddScoped<IDirectoryEntryRepository, DirectoryEntryRepository>();
            services.AddScoped<IDirectoryEntriesAuditRepository, DirectoryEntriesAuditRepository>();
            services.AddScoped<IDirectoryEntrySelectionRepository, DirectoryEntrySelectionRepository>();
            services.AddScoped<ITrafficLogRepository, TrafficLogRepository>();
            services.AddScoped<IExcludeUserAgentRepository, ExcludeUserAgentRepository>();
            services.AddScoped<IContentSnippetRepository, ContentSnippetRepository>();
            services.AddScoped<IProcessorConfigRepository, ProcessorConfigRepository>();
            services.AddScoped<IBlockedIPRepository, BlockedIPRepository>();

            return services;
        }
    }
}