using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]
        required public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        required public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}
