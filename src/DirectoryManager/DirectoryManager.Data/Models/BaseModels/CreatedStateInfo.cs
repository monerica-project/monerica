using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DirectoryManager.Data.Models.BaseModels
{
    public class CreatedStateInfo
    {
        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }
    }
}
