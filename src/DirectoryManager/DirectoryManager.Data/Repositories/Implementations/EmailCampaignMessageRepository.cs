using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailCampaignMessageRepository : IEmailCampaignMessageRepository
    {
        private readonly IApplicationDbContext context;

        public EmailCampaignMessageRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public IEnumerable<EmailCampaignMessage> GetMessagesByCampaign(int campaignId)
        {
            return this.context.EmailCampaignMessages
                           .Include(cm => cm.EmailMessage)
                           .Where(cm => cm.EmailCampaignId == campaignId)
                           .OrderBy(cm => cm.SequenceOrder)
                           .ToList();
        }

        public EmailCampaignMessage? GetNextMessage(int campaignId, int sequenceOrder)
        {
            return this.context.EmailCampaignMessages
                           .Include(cm => cm.EmailMessage)
                           .Where(cm => cm.EmailCampaignId == campaignId && cm.SequenceOrder > sequenceOrder)
                           .OrderBy(cm => cm.SequenceOrder)
                           .FirstOrDefault();
        }

        public EmailCampaignMessage Create(EmailCampaignMessage campaignMessage)
        {
            this.context.EmailCampaignMessages.Add(campaignMessage);
            this.context.SaveChanges();
            return campaignMessage;
        }

        public bool Delete(int campaignMessageId)
        {
            var message = this.context.EmailCampaignMessages.Find(campaignMessageId);
            if (message == null)
            {
                return false;
            }

            this.context.EmailCampaignMessages.Remove(message);
            return this.context.SaveChanges() > 0;
        }
    }
}