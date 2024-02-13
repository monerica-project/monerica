using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class EmailSubscription : StateInfo
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailSubscriptionId { get; set; }

        [StringLength(100)]
        [Required]
        public string Email { get; set; } = string.Empty;

        public bool IsSubscribed { get; set; }
    }
}