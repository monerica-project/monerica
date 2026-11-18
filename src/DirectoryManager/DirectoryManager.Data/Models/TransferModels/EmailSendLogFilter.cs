namespace DirectoryManager.Data.Models.TransferModels
{
    /// <summary>Filters for the admin email send log list. Empty/null = not filtered.</summary>
    public class EmailSendLogFilter
    {
        public string? Source { get; set; }

        public string? RecipientContains { get; set; }

        public string? SubjectContains { get; set; }

        /// <summary>True = sent, false = failed, null = both.</summary>
        public bool? IsSuccess { get; set; }

        public int? ContentId { get; set; }
    }
}
