using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailCampaignRepository
    {
        EmailCampaign? Get(int campaignId);
        IEnumerable<EmailCampaign> GetAll();
        EmailCampaign Create(EmailCampaign campaign);
        bool Update(EmailCampaign campaign);
        bool Delete(int campaignId);
    }
}