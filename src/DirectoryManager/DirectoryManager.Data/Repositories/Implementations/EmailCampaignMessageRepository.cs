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

            // Delete the specified message
            this.context.EmailCampaignMessages.Remove(message);
            this.context.SaveChanges();

            // Re-sequence remaining messages in the campaign
            this.ReorderMessages(message.EmailCampaignId);

            return true;
        }

        private void ReorderMessages(int campaignId)
        {
            // Get all messages for the campaign ordered by SequenceOrder
            var remainingMessages = this.context.EmailCampaignMessages
                                      .Where(cm => cm.EmailCampaignId == campaignId)
                                      .OrderBy(cm => cm.SequenceOrder)
                                      .ToList();

            // Reset the SequenceOrder to be sequential starting from 1
            for (int i = 0; i < remainingMessages.Count; i++)
            {
                remainingMessages[i].SequenceOrder = i + 1;
            }

            // Save the updated sequence to the database
            this.context.SaveChanges();
        }
    }
}