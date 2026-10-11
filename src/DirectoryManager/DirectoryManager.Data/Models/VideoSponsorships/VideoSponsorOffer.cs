using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.VideoSponsorships
{
    // A purchasable kind of video sponsorship (e.g. a 1-minute segment inside an upcoming
    // video, or a dedicated video). Prices are edited in the admin, never hard-coded.
    public class VideoSponsorOffer : StateInfo
    {
        [Key]
        public int VideoSponsorOfferId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        // Price in USD for ONE video of this type.
        public decimal PriceUsd { get; set; }

        // True = the buyer picks one or more upcoming videos from the queue (priced per video).
        // False = a single stand-alone video the buyer describes in their notes.
        public bool RequiresVideoSelection { get; set; }

        public bool IsEnabled { get; set; } = true;

        public int SortOrder { get; set; }
    }
}
