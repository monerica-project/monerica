using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class EditListingViewModel
    {
        public int Id { get; set; }
        [Required]
        public DateTime CampaignStartDate { get; set; }

        [Required]
        public DateTime CampaignEndDate { get; set; }

        public SponsorshipType SponsorshipType { get; set; }
    }
}