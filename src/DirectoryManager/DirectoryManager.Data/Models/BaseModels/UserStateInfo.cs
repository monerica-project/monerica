using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Data.Models.BaseModels
{
    public class UserStateInfo : StateInfo
    {
        [StringLength(36)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [StringLength(36)]
        public string? UpdatedByUserId { get; set; }
    }
}
