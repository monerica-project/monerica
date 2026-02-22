using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.Reviews
{
    public class ReviewerKey : UserStateInfo
    {
        [Key]
        public int ReviewerKeyId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Fingerprint { get; set; } = string.Empty;

        [Required]
        public string PublicKeyBlock { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? Alias { get; set; }
    }
}