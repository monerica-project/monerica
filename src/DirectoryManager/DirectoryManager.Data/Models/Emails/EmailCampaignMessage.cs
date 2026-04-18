using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    public class EmailCampaignMessage : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailCampaignMessageId { get; set; }

        [ForeignKey("EmailCampaign")]
        public int EmailCampaignId { get; set; }

        public EmailCampaign EmailCampaign { get; set; } = null!;

        [ForeignKey("EmailMessage")]
        public int EmailMessageId { get; set; }

        public EmailMessage EmailMessage { get; set; } = null!;

        // Order in the sequence for the campaign
        public int SequenceOrder { get; set; }
    }
}