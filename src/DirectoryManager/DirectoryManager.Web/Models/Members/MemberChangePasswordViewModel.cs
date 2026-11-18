using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberChangePasswordViewModel
    {
        [Display(Name = "Current password")]
        [StringLength(256)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "New password")]
        [StringLength(256)]
        public string? NewPassword { get; set; }
    }
}
