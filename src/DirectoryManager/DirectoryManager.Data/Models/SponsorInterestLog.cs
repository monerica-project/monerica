using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.BaseModels;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// Someone looked at sponsoring a listing (options page, prices, confirm page or payment).
    /// Used to gauge sponsorship interest over time. Deliberately stores NO IP address or visitor
    /// identity: only which listing, which placement, which step and when.
    /// </summary>
    [Index(nameof(CreateDate))]
    [Index(nameof(DirectoryEntryId), nameof(CreateDate))]
    public class SponsorInterestLog : CreatedStateInfo
    {
        public int Id { get; set; }

        public int DirectoryEntryId { get; set; }

        /// <summary>Unknown on the options page (no placement chosen yet).</summary>
        public SponsorshipType SponsorshipType { get; set; }

        /// <summary>Category or subcategory id for category / subcategory placements.</summary>
        public int? TypeId { get; set; }

        public SponsorInterestStep Step { get; set; }
    }
}
