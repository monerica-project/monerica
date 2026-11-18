namespace DirectoryManager.Web.Models
{
    public class TwoFactorRecoveryCodesViewModel
    {
        public List<string> Codes { get; set; } = new ();

        public bool JustEnabled { get; set; }
    }
}
