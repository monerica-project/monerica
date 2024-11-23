using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailCampaignRepository : IEmailCampaignRepository
    {
        private readonly IApplicationDbContext context;

        public EmailCampaignRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public EmailCampaign? Get(int campaignId)
        {
            return this.context.EmailCampaigns
                           .Include(c => c.CampaignMessages)
                               .ThenInclude(cm => cm.EmailMessage)
                           .FirstOrDefault(c => c.EmailCampaignId == campaignId);
        }

        public IEnumerable<EmailCampaign> GetAll(int pageIndex, int pageSize, out int totalItems)
        {
            totalItems = this.context.EmailCampaigns.Count();
            return this.context.EmailCampaigns
                           .Include(c => c.CampaignMessages)
                           .OrderBy(c => c.EmailCampaignId)
                           .Skip(pageIndex * pageSize)
                           .Take(pageSize)
                           .ToList();
        }

        public EmailCampaign? GetDefault()
        {
            return this.context.EmailCampaigns.Where(x => x.IsDefault == true).FirstOrDefault();
        }

        public EmailCampaign Create(EmailCampaign campaign)
        {
            if (campaign.IsDefault)
            {
                // Ensure there is only one default campaign
                this.UnsetOtherDefaults();
            }

            this.context.EmailCampaigns.Add(campaign);
            this.context.SaveChanges();
            return campaign;
        }

        public bool Update(EmailCampaign campaign)
        {
            if (campaign.IsDefault)
            {
                // Ensure there is only one default campaign
                this.UnsetOtherDefaults(campaign.EmailCampaignId);
            }

            this.context.EmailCampaigns.Update(campaign);
            return this.context.SaveChanges() > 0;
        }

        public bool Delete(int campaignId)
        {
            var campaign = this.context.EmailCampaigns.Find(campaignId);
            if (campaign == null)
            {
                return false;
            }

            this.context.EmailCampaigns.Remove(campaign);
            return this.context.SaveChanges() > 0;
        }

        public IEnumerable<EmailCampaignMessage> GetOrderedMessages(int campaignId)
        {
            return this.context.EmailCampaignMessages
                          .Where(m => m.EmailCampaignId == campaignId)
                          .OrderBy(m => m.SequenceOrder)
                          .ToList();
        }

        private void UnsetOtherDefaults(int? excludeCampaignId = null)
        {
            var otherDefaultCampaigns = this.context.EmailCampaigns
                .Where(c => c.IsDefault && (excludeCampaignId == null || c.EmailCampaignId != excludeCampaignId))
                .ToList();

            foreach (var campaign in otherDefaultCampaigns)
            {
                campaign.IsDefault = false;
            }

            this.context.SaveChanges();
        }
    }
}