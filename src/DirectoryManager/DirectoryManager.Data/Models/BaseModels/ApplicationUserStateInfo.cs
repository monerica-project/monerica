using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace DirectoryManager.Data.Models.BaseModels
{
    public class ApplicationUserStateInfo : IdentityUser
    {
        public DateTime CreateDate { get; set; }

        public DateTime? UpdateDate { get; set; }
    }
}