using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    /// <summary>One "additional subcategory" slot on the admin listing form.</summary>
    public class AdditionalSubcategoryInput
    {
        public int SubCategoryId { get; set; }

        [MaxLength(255)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Link { get; set; }

        [MaxLength(500)]
        public string? LinkA { get; set; }

        [MaxLength(500)]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        public string? Link2A { get; set; }

        [MaxLength(500)]
        public string? Link3 { get; set; }

        [MaxLength(500)]
        public string? Link3A { get; set; }

        public string? Description { get; set; }

        public IEnumerable<string?> Links() => new[] { this.Link, this.LinkA, this.Link2, this.Link2A, this.Link3, this.Link3A };
    }
}
