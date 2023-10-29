using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class SponsoredListingOffer : UserStateInfo
    {
        [Key]
        public int Id { get; set; }

        public bool IsEnabled { get; set; }

        [Required]
        [MaxLength(255)]
        required public string Description { get; set; }

        public int Days { get; set; }

        public Currency PriceCurrency { get; set; }

        public decimal Price { get; set; }
    }
}