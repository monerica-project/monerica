using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class EditListingViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime CampaignStartDate { get; set; }

        [Required]
        public DateTime CampaignEndDate { get; set; }

        public SponsorshipType SponsorshipType { get; set; }
    }
}