namespace DirectoryManager.Web.Models
{
    public class TwoFactorStatusViewModel
    {
        public bool IsEnabled { get; set; }

        public int RecoveryCodesLeft { get; set; }
    }
}
