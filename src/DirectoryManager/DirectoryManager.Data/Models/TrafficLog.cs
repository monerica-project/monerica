using System.ComponentModel.DataAnnotations;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    public class TrafficLog : CreatedStateInfo
    {
        [Key]
        public int TrafficLogId { get; set; }
        required public string IpAddress { get; set; }
        required public string Url { get; set; }
        required public string UserAgent { get; set; }
    }
}