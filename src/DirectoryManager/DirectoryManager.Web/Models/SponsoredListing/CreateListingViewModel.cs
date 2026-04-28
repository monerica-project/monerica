using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class CreateListingViewModel
    {
        [Required]
        public int DirectoryEntryId { get; set; }

        [Required]
        public SponsorshipType SponsorshipType { get; set; }

        public int? SubCategoryId { get; set; }

        public int? CategoryId { get; set; }

        [Required]
        public DateTime CampaignStartDate { get; set; }

        [Required]
        public DateTime CampaignEndDate { get; set; }
    }
}