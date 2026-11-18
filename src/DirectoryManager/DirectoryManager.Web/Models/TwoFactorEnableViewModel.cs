namespace DirectoryManager.Web.Models
{
    public class TwoFactorEnableViewModel
    {
        public string SharedKey { get; set; } = string.Empty;

        public string AuthenticatorUri { get; set; } = string.Empty;

        public string QrCodeDataUri { get; set; } = string.Empty;

        public string Issuer { get; set; } = string.Empty;

        public string AccountName { get; set; } = string.Empty;
    }
}
