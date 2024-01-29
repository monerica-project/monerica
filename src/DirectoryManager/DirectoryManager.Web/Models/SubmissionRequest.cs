using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models
{
    public class SubmissionRequest
    {
        public int? SubmissionId { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Link", Prompt = "https://yoursite.net")]
        public string Link { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Display(Name = "Name", Prompt = "Company or project name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Description", Prompt = "Describe your listing")]
        public string? Description { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link 2", Prompt = "Link 2")]
        public string? Link2 { get; set; }

        [MaxLength(500)]
        [Display(Name = "Link 3", Prompt = "Link 3")]
        public string? Link3 { get; set; }

        [MaxLength(255)]
        [Display(Name = "Contact", Prompt = "@yourname on Twitter/ GitHub, etc.")]
        public string? Contact { get; set; }

        [MaxLength(255)]
        [Display(Name = "Location", Prompt = "City, Region, Country (example: New York, NY, USA)")]
        public string? Location { get; set; }

        [MaxLength(255)]
        [Display(Name = "Processor", Prompt = "Payment processing company/ plugin")]
        public string? Processor { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Note", Prompt = "Notes about listing you want displayed")]
        public string? Note { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Note To Admin", Prompt = "Notes to admin reviewing submission")]
        public string? NoteToAdmin { get; set; }

        [Display(Name = "SubCategoryId", Prompt = "Select sub category")]
        public int? SubCategoryId { get; set; }

        [MaxLength(255)]
        [Display(Name = "Suggested Category", Prompt = "New category > New sub category")]
        public string? SuggestedSubCategory { get; set; }

        public int? DirectoryEntryId { get; set; }

        [Display(Name = "Suggested Status", Prompt = "Status")]
        public DirectoryStatus? DirectoryStatus { get; set; }
    }
}