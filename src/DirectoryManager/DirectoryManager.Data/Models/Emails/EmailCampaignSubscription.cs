using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models.Emails
{
    public class EmailCampaignSubscription
    {
        [Key]
        public int EmailCampaignSubscriptionId { get; set; }

        [ForeignKey("EmailCampaign")]
        public int EmailCampaignId { get; set; }
        public EmailCampaign EmailCampaign { get; set; } = null!;

        [ForeignKey("EmailSubscription")]
        public int EmailSubscriptionId { get; set; }
        public EmailSubscription EmailSubscription { get; set; } = null!;

        public DateTime SubscribedDate { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true; // Track if the subscription is active
    }
}