using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Emails
{
    public class EmailCampaign : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailCampaignId { get; set; }

        [StringLength(100)]
        [Required]
        public string Name { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        // Defines the number of days between messages in the campaign sequence
        public int IntervalDays { get; set; }

        // Start date for the campaign if needed
        public DateTime? StartDate { get; set; }

        // Collection of campaign messages with a defined sequence order
        public virtual ICollection<EmailCampaignMessage> CampaignMessages { get; set; } = new List<EmailCampaignMessage>();
    }
}