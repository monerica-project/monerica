using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailCampaignRepository
    {
        EmailCampaign? Get(int campaignId);
        EmailCampaign? GetByKey(string emailCampaignKey);
        EmailCampaign? GetDefault();
        IEnumerable<EmailCampaign> GetAll(int pageIndex, int pageSize, out int totalItems);
        EmailCampaign Create(EmailCampaign campaign);
        bool Update(EmailCampaign campaign);
        bool Delete(int campaignId);
        IEnumerable<EmailCampaignMessage> GetOrderedMessages(int campaignId);
    }
}