using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class SubCategory : UserStateInfo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string SubCategoryKey { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }

        // Foreign Key for Category
        [Required]
        public int CategoryId { get; set; }

        // Navigation Property for the parent Category
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
    }
}
