using DirectoryManager.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class SubmissionRequest
    {
        [Required]
        [MaxLength(255)]
        [Display(Name = "Name", Prompt = "* Enter the name")]
        public string Name { get; set; }

        [Required]
        [Url]
        [MaxLength(500)]
        [Display(Name = "Link", Prompt = "* Enter the link URL")]
        public string Link { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Description", Prompt = "* Enter a description")]
        public string Description { get; set; }

        [MaxLength(255)]
        [Display(Name = "Location", Prompt = "Enter the location")]
        public string? Location { get; set; }

        [MaxLength(255)]
        [Display(Name = "Processor", Prompt = "Enter the processor")]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Note", Prompt = "Add any additional notes")]
        public string? Note { get; set; }

        [Display(Name = "Category", Prompt = "Add Category")]
        public Category? Category { get; set; }

        [MaxLength(255)]
        [Display(Name = "Suggested Category", Prompt = "Add Category")]
        public string SuggestedCategory { get; set; }

        [Display(Name = "Sub Category", Prompt = "Add SubCategory")]
        public SubCategory? SubCategory { get; set; }

        [MaxLength(255)]
        [Display(Name = "Suggested Sub Category", Prompt = "Add Sub Category")]
        public string SuggestedSubCategory { get; set; }
    }
}
