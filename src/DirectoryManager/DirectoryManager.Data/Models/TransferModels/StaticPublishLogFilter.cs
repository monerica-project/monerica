namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>Filter for the static-publish log on the admin page.</summary>
    public class StaticPublishLogFilter
    {
        /// <summary>Exact path, or a prefix when it ends with "*".</summary>
        public string? Path { get; set; }

        public string? Result { get; set; }

        public string? Source { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 50;
    }
}
