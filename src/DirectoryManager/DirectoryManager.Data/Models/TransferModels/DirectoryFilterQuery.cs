using DirectoryManager.Data.Enums;

namespace DirectoryManager.Data.Models.TransferModels
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

        // Free-text search. Matches name, description, tags, and
        // category/subcategory names. Null/empty = no text filter.
        // NOTE: named SearchTerm (not "Q") so it never collides with the
        // controller's action parameter "q" during model binding.
        public string? SearchTerm { get; set; }

        public DirectoryFilterSort Sort { get; set; } = DirectoryFilterSort.TopRated;

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 24;

        public List<int>? TagIds { get; set; } = new ();

        // Populated by the controller (NOT from querystring): the set of active
        // sponsor DirectoryEntryIds. Used by the TopRated sort to pin sponsors
        // to the very top. Null/empty = no pinning.
        public HashSet<int>? SponsoredEntryIds { get; set; }
    }
}