using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.SponsoredListings
{
    public class ProcessorConfig : UserStateInfo
    {
        [Key]
        public int ProcessorConfigId { get; set; }

        public PaymentProcessor PaymentProcessor { get; set; }

        public bool UseProcessor { get; set; }

        public string Configuration { get; set; } = string.Empty;
    }
}