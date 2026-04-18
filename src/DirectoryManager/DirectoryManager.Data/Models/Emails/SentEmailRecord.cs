using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    public class SentEmailRecord : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SentEmailRecordId { get; set; }

        // Foreign Key to EmailSubscription
        [ForeignKey("EmailSubscription")]
        public int EmailSubscriptionId { get; set; }

        public EmailSubscription EmailSubscription { get; set; } = null!;

        // Foreign Key to EmailMessage
        [ForeignKey("EmailMessage")]
        public int EmailMessageId { get; set; }

        public EmailMessage EmailMessage { get; set; } = null!;

        // Date when the email was sent
        public DateTime SentDate { get; set; } = DateTime.UtcNow;

        // Additional status field to track delivery status if needed
        public bool IsDelivered { get; set; } = true;
    }
}