namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>One per-path result reported by the static-site publisher.</summary>
    public class StaticPublishResultRow
    {
        public string Path { get; set; } = string.Empty;

        public string Result { get; set; } = string.Empty;

        public string? Error { get; set; }

        public long DurationMs { get; set; }
    }
}
