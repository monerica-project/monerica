using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models
{
    public class LogEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogEntryId { get; set; }

        public string? Message { get; set; }

        public string? MessageTemplate { get; set; }

        public string? Level { get; set; }

        [Required]
        public DateTimeOffset TimeStamp { get; set; }

        public string? Exception { get; set; }

        public string? Properties { get; set; }
    }
}
