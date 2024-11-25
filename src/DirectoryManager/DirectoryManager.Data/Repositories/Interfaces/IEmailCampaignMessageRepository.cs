using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailCampaignMessageRepository
    {
        IEnumerable<EmailCampaignMessage> GetMessagesByCampaign(int campaignId);
        EmailCampaignMessage? GetNextMessage(int campaignId, int sequenceOrder);
        EmailCampaignMessage Create(EmailCampaignMessage campaignMessage);
        bool Delete(int campaignMessageId);
    }
}