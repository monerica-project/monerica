using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SubmissionRequest
    {
        [Required]
        [Url]
        [MaxLength(500)]
        [Display(Name = "Link", Prompt = "https://yoursite.net")]
        required public string Link { get; set; }

        [Required]
        [MaxLength(255)]
        [Display(Name = "Name", Prompt = "Company/ Project Name")]
        required public string Name { get; set; }

        [MaxLength(500)]
        [Display(Name = "Description", Prompt = "People should know...")]
        public string? Description { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link 2", Prompt = "Onion address")]
        public string? Link2 { get; set; }

        [MaxLength(255)]
        [Display(Name = "Contact", Prompt = "@yourname on Twitter/ GitHub, etc.")]
        public string? Contact { get; set; }

        [MaxLength(255)]
        [Display(Name = "Location", Prompt = "New York, NY, USA")]
        public string? Location { get; set; }

        [MaxLength(255)]
        [Display(Name = "Processor", Prompt = "Payment processing company")]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Note", Prompt = "Add any additional notes")]
        public string? Note { get; set; }

        [Display(Name = "SubCategoryId", Prompt = "Select Sub Category")]
        public int? SubCategoryId { get; set; }

        [MaxLength(255)]
        [Display(Name = "Suggested Category", Prompt = "New Category > New Sub Category")]
        public string? SuggestedSubCategory { get; set; }

        public int? DirectoryEntryId { get; set; }

        [Display(Name = "Suggested Status", Prompt = "Status")]
        public DirectoryStatus? DirectoryStatus { get; set; }
    }
}