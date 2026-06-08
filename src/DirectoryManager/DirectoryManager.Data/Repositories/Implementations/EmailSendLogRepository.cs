using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailSendLogRepository : IEmailSendLogRepository
    {
        // Defensive caps so a pathological subject/error never blows the column.
        private const int MaxSubjectLength = 500;
        private const int MaxRecipientLength = 320;

        private readonly IApplicationDbContext context;

        public EmailSendLogRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public EmailSendLog Create(EmailSendLog model)
        {
            try
            {
                this.context.EmailSendLogs.Add(model);
                this.context.SaveChanges();
                return model;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public void Log(
            string sourceApplication,
            string recipientEmail,
            int recipientCount,
            string subject,
            bool isSuccess,
            int? statusCode,
            string? errorMessage)
        {
            var model = new EmailSendLog
            {
                SourceApplication = Truncate(sourceApplication, 100),
                RecipientEmail = Truncate(recipientEmail, MaxRecipientLength),
                RecipientCount = recipientCount < 1 ? 1 : recipientCount,
                Subject = Truncate(subject, MaxSubjectLength),
                IsSuccess = isSuccess,
                StatusCode = statusCode,
                ErrorMessage = errorMessage,
                SentDate = DateTime.UtcNow
            };

            this.context.EmailSendLogs.Add(model);
            this.context.SaveChanges();
        }

        public EmailSendLog? Get(int emailSendLogId)
        {
            try
            {
                return this.context.EmailSendLogs.Find(emailSendLogId);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<EmailSendLog> GetRecent(int count)
        {
            try
            {
                return this.context.EmailSendLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.SentDate)
                    .Take(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<EmailSendLog> GetPagedRecords(int pageIndex, int pageSize, out int totalRecords)
        {
            try
            {
                totalRecords = this.context.EmailSendLogs.Count();

                return this.context.EmailSendLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.SentDate)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<EmailSendLog> GetBySource(string sourceApplication, int pageIndex, int pageSize, out int totalRecords)
        {
            try
            {
                var query = this.context.EmailSendLogs
                    .AsNoTracking()
                    .Where(x => x.SourceApplication == sourceApplication);

                totalRecords = query.Count();

                return query
                    .OrderByDescending(x => x.SentDate)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
