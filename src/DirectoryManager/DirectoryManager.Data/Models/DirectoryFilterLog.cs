using DirectoryManager.Data.Models.BaseModels;

namespace DirectoryManager.Data.Models
{
    /// <summary>
    /// One applied directory-filter action. Deliberately stores NO IP address —
    /// only what the user filtered by (decoded to names in the admin report).
    /// </summary>
    public class DirectoryFilterLog : CreatedStateInfo
    {
        public int Id { get; set; }

        public int? CategoryId { get; set; }

        public int? SubCategoryId { get; set; }

        /// <summary>Comma-separated tag IDs (multi-select).</summary>
        public string? TagIds { get; set; }

        public string? SearchTerm { get; set; }

        /// <summary>ISO-2 country code.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Comma-separated DirectoryStatus names.</summary>
        public string? Statuses { get; set; }

        public bool HasVideo { get; set; }

        public bool HasTor { get; set; }

        public bool HasI2p { get; set; }

        public int Page { get; set; }
    }
}
