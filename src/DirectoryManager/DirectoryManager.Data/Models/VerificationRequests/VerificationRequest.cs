using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models.VerificationRequests
{
    // A public, captcha-gated request asking Monerica to verify a given listing.
    public class VerificationRequest : StateInfo
    {
        [Key]
        public int VerificationRequestId { get; set; }

        [Required]
        public int DirectoryEntryId { get; set; }

        public DirectoryEntry DirectoryEntry { get; set; } = null!;

        // Required reason the requester gives for wanting this listing verified.
        [Required]
        public string Comment { get; set; } = string.Empty;

        public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;

        // Abuse signal without storing raw IP: HMAC(IP) hex.
        [MaxLength(64)]
        public string? SourceIpHash { get; set; }

    }
}
