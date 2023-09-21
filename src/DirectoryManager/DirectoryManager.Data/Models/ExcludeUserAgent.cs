using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class ExcludeUserAgent : CreatedStateInfo
    {
        [Key]
        public int ExcludeUserAgentId { get; set; }
        required public string UserAgent { get; set; }
    }
}
