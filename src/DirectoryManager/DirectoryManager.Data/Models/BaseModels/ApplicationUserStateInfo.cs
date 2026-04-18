using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace DirectoryManager.Data.Models.BaseModels
{
    public class ApplicationUserStateInfo : IdentityUser
    {
        [Column(TypeName = "datetime2")]
        public DateTime CreateDate { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? UpdateDate { get; set; }
    }
}