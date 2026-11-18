using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberSignInViewModel
    {
        [Display(Name = "Username")]
        [StringLength(64)]
        public string? Username { get; set; }

        [Display(Name = "Password")]
        [StringLength(256)]
        public string? Password { get; set; }

        [Display(Name = "Keep me signed in on this device")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
