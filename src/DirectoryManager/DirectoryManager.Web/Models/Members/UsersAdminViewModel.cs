using DirectoryManager.Data.Models.Reviews;

namespace DirectoryManager.Web.Models.Members
{
    public sealed class UsersAdminViewModel
    {
        public UsersAdminQuery Query { get; set; } = new ();

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int Total { get; set; }

        public List<AccountRow> Accounts { get; set; } = new ();

        public List<KeyRow> Keys { get; set; } = new ();

        /// <summary>Keys currently on the block list (keys tab).</summary>
        public List<BlockedReviewerKey> ActiveKeyBlocks { get; set; } = new ();

        /// <summary>Builds a list URL with the current filters and the given overrides.</summary>
        public string Url(string? tab = null, int? page = null, string? sort = null)
        {
            var parts = new List<string> { "tab=" + Uri.EscapeDataString(tab ?? this.Query.Tab) };
            if (tab == null || tab == this.Query.Tab)
            {
                AddPart(parts, "q", this.Query.Q);
                AddPart(parts, "key", this.Query.Key);
                AddPart(parts, "match", this.Query.Match == UsersAdminMatch.Contains ? null : this.Query.Match);
                AddPart(parts, "status", this.Query.Status);
                var s = sort ?? this.Query.Sort;
                AddPart(parts, "sort", s == UsersAdminSorts.Newest ? null : s);
                if ((page ?? 1) > 1)
                {
                    parts.Add("page=" + page);
                }
            }

            return "/admin/users?" + string.Join("&", parts);
        }

        private static void AddPart(List<string> parts, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add(name + "=" + Uri.EscapeDataString(value));
            }
        }
    }
}
