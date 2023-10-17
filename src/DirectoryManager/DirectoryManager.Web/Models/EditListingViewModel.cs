using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class EditListingViewModel
    {
        public int Id { get; set; }
        [Required]
        public DateTime CampaignStartDate { get; set; }
        [Required]
        public DateTime CampaignEndDate { get; set; }
    }
}