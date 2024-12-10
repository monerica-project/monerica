namespace DirectoryManager.NewsletterSender.Services.Interfaces
{
    public interface IEmailCampaignProcessingService
    {
        Task ProcessCampaignsAsync();
    }
}