namespace DirectoryManager.Web.Models.Emails
{
    public class EmailCampaignMessageModel
    {
        public int EmailMessageId { get; set; }

        // Position in the campaign sequence
        public int SequenceOrder { get; set; }

        public int EmailCampaignMessageId { get; set; }
    }
}