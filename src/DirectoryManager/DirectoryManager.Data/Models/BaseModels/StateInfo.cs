using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models.BaseModels
{
    public class StateInfo : CreatedStateInfo
    {
        public DateTime? UpdateDate { get; set; }
    }
}
