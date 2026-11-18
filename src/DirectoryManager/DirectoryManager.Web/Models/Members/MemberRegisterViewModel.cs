using System.ComponentModel.DataAnnotations;

namespace DirectoryManager.Web.Models.Members
{
    public class MemberRegisterViewModel
    {
        [Display(Name = "Username")]
        [StringLength(64)]
        public string? Username { get; set; }

        // Signed proof that the site assigned Username (see MemberAssignedUsername). The posted
        // Username field is ignored; only the name inside this token is used.
        [StringLength(2048)]
        public string? UsernameToken { get; set; }

        [Display(Name = "Password")]
        [StringLength(256)]
        public string? Password { get; set; }

        /// <summary>The visitor ticked "I have saved my username and password".</summary>
        public bool SavedCredentials { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
