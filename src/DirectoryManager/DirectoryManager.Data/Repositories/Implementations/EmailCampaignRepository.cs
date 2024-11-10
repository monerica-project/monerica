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
                           .FirstOrDefault(c => c.EmailCampaignId == campaignId);
        }

        public IEnumerable<EmailCampaign> GetAll()
        {
            return this.context.EmailCampaigns
                           .Include(c => c.CampaignMessages)
                           .ToList();
        }

        public EmailCampaign Create(EmailCampaign campaign)
        {
            this.context.EmailCampaigns.Add(campaign);
            this.context.SaveChanges();
            return campaign;
        }

        public bool Update(EmailCampaign campaign)
        {
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
    }
}