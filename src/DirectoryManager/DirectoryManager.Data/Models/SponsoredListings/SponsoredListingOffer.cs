using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class SponsoredListingOffer : UserStateInfo
    {
        [Key]
        public int SponsoredListingOfferId { get; set; }

        public bool IsEnabled { get; set; }

        [Required]
        [MaxLength(255)]
        required public string Description { get; set; }

        public int Days { get; set; }

        public Currency PriceCurrency { get; set; }

        public decimal Price { get; set; }

        public virtual Subcategory? Subcategory { get; set; }

        public int? SubcategoryId { get; set; }

        public SponsorshipType SponsorshipType { get; set; } = SponsorshipType.Unknown;
    }
}