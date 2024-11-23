namespace DirectoryManager.ScheduledNotifier.Services.Interfaces
{
    public interface IEmailCampaignProcessingService
    {
        /// <summary>
        /// Processes a campaign by sending messages sequentially to subscribers who haven't received them.
        /// </summary>
        /// <param name="campaignId">The ID of the email campaign to process.</param>
        void ProcessCampaign(int campaignId);
    }
}
