using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberDeleteAccountViewModel
    {
        [Display(Name = "Your password")]
        [StringLength(256)]
        public string? Password { get; set; }

        [Display(Name = "Yes, delete my account")]
        public bool Confirm { get; set; }
    }
}
