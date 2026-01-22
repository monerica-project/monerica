using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models
{
    public class DirectoryFilterQuery
    {
        // Querystring names: statuses=Admitted&statuses=Verified etc.
        public List<DirectoryStatus>? Statuses { get; set; }

        public string? Country { get; set; } // ISO2, or null/empty = all

        public bool HasVideo { get; set; }
        public bool HasTor { get; set; }
        public bool HasI2p { get; set; }

        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }

        public DirectoryFilterSort Sort { get; set; } = DirectoryFilterSort.Newest;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
