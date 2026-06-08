using DirectoryManager.Data.Models.Emails;

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
            string? errorMessage);

        EmailSendLog? Get(int emailSendLogId);

        IList<EmailSendLog> GetRecent(int count);

        IList<EmailSendLog> GetPagedRecords(int pageIndex, int pageSize, out int totalRecords);

        IList<EmailSendLog> GetBySource(string sourceApplication, int pageIndex, int pageSize, out int totalRecords);
    }
}
