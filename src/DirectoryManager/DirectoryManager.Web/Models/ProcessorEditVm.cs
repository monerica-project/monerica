using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Processors
{
    public class ProcessorEditVm
    {
        public int ProcessorId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}