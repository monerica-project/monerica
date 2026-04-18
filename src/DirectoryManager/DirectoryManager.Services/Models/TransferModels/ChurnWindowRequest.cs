// ChurnWindowRequest.cs
using System;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Services.Models.TransferModels
{
    /// <summary>
    /// Input for computing churn over an inclusive day window [StartUtc, EndUtc].
    /// </summary>
    public sealed class ChurnWindowRequest
    {
        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public SponsorshipType? SponsorshipType { get; set; }

        public int? CategoryId { get; set; }

        public int? SubCategoryId { get; set; }
    }
}
