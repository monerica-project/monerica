using System.ComponentModel.DataAnnotations;
using DirectoryManager.Web.Attributes;

namespace DirectoryManager.Web.Models.Reviews
{
    public class RaffleEnterInputModel
    {
        // Hidden — always XMR, not shown to the user
        public string CryptoType { get; set; } = "XMR";

        [Required(ErrorMessage = "Please enter your Monero address.")]
        [MaxLength(512)]
        [MoneroAddress]
        [Display(Name = "Monero (XMR) Address")]
        public string CryptoAddress { get; set; } = string.Empty;
    }
}