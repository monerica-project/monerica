using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.TransferModels;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailSendLogRepository
    {
        EmailSendLog Create(EmailSendLog model);

        /// <summary>
        /// Convenience helper that builds and persists a single send-log row.
        /// Used by EmailService on every outbound message.
        /// </summary>
        void Log(
            string sourceApplication,
            string recipientEmail,
            int recipientCount,
            string subject,
            bool isSuccess,
            int? statusCode,
            string? errorMessage,
            int? emailSendLogContentId = null);

        EmailSendLog? Get(int emailSendLogId);

        IList<EmailSendLog> GetRecent(int count);

        IList<EmailSendLog> GetPagedRecords(int pageIndex, int pageSize, out int totalRecords);

        IList<EmailSendLog> GetBySource(string sourceApplication, int pageIndex, int pageSize, out int totalRecords);

        /// <summary>Newest first, with every non-empty filter applied (AND).</summary>
        IList<EmailSendLog> Search(EmailSendLogFilter filter, int pageIndex, int pageSize, out int totalRecords);

        /// <summary>One row with its stored content (if any).</summary>
        EmailSendLog? GetWithContent(int emailSendLogId);

        /// <summary>Log rows (sends) that used the given content.</summary>
        int CountByContent(int emailSendLogContentId);
    }
}
