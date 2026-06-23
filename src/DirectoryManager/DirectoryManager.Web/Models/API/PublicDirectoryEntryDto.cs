using System;
using System.Collections.Generic;
using DirectoryManager.Data.Enums;

namespace DirectoryManager.Web.Models.API
{
    /// <summary>
    /// Public, serialization-safe projection of a <see cref="DirectoryManager.Data.Models.DirectoryEntry"/>
    /// for the anonymous /api/all endpoint.
    ///
    /// This type exists specifically so the API can NEVER leak fields that are not meant
    /// to be public. The endpoint must project entities into this DTO rather than returning
    /// the EF entity directly — otherwise any column later added to the entity is exposed
    /// automatically.
    ///
    /// Deliberately EXCLUDED (do not add back):
    ///   - LinkA / Link2A / Link3A  (affiliate/monetization links — hidden from users)
    ///   - CreatedByUserId / UpdatedByUserId (internal admin identifiers)
    ///
    /// Judgment calls (safe to remove if you want a leaner feed):
    ///   - PgpKey                   (a PUBLIC key, already published on each listing)
    ///   - CreateDate / UpdateDate  (already shown on the site)
    /// </summary>
    public sealed class PublicDirectoryEntryDto
    {
        public int DirectoryEntryId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string DirectoryEntryKey { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;

        public string? Link2 { get; set; }

        public string? Link3 { get; set; }

        public string? Description { get; set; }

        public string? Note { get; set; }

        public string? Location { get; set; }

        public string? Processor { get; set; }

        public string? CountryCode { get; set; }

        public DirectoryStatus DirectoryStatus { get; set; }

        public DirectoryBadge DirectoryBadge { get; set; }

        public DateOnly? FoundedDate { get; set; }

        public string? ProofLink { get; set; }

        public string? VideoLink { get; set; }

        public bool ReviewsDisabled { get; set; }

        public string? PgpKey { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime? UpdateDate { get; set; }

        public PublicSubcategoryDto? SubCategory { get; set; }

        public List<string> Tags { get; set; } = new ();
    }
}
