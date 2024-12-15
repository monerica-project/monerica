using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Emails
{
    public class EmailCampaignModel
    {
        public int EmailCampaignId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, 365, ErrorMessage = "Interval days should be between 0 and 365.")]
        public int IntervalDays { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        public bool IsDefault { get; set; }

        public bool SendMessagesPriorToSubscription { get; set; }

        // Collection of campaign messages to define the sequence
        public List<EmailCampaignMessageModel> CampaignMessages { get; set; } = new List<EmailCampaignMessageModel>();
    }
}