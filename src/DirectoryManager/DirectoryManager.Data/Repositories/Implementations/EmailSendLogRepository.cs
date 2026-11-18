using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Models.TransferModels;
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
            string? errorMessage,
            int? emailSendLogContentId = null)
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
                SentDate = DateTime.UtcNow,
                EmailSendLogContentId = emailSendLogContentId,
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

        public IList<EmailSendLog> Search(EmailSendLogFilter filter, int pageIndex, int pageSize, out int totalRecords)
        {
            ArgumentNullException.ThrowIfNull(filter);
            IQueryable<EmailSendLog> query = this.context.EmailSendLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Source))
            {
                var source = filter.Source.Trim();
                query = query.Where(x => x.SourceApplication == source);
            }

            if (!string.IsNullOrWhiteSpace(filter.RecipientContains))
            {
                var r = filter.RecipientContains.Trim().ToLowerInvariant();
                query = query.Where(x => x.RecipientEmail.ToLower().Contains(r));
            }

            if (!string.IsNullOrWhiteSpace(filter.SubjectContains))
            {
                var sub = filter.SubjectContains.Trim().ToLowerInvariant();
                query = query.Where(x => x.Subject.ToLower().Contains(sub));
            }

            if (filter.IsSuccess.HasValue)
            {
                var ok = filter.IsSuccess.Value;
                query = query.Where(x => x.IsSuccess == ok);
            }

            if (filter.ContentId.HasValue)
            {
                var cid = filter.ContentId.Value;
                query = query.Where(x => x.EmailSendLogContentId == cid);
            }

            totalRecords = query.Count();

            return query
                .OrderByDescending(x => x.SentDate)
                .ThenByDescending(x => x.EmailSendLogId)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public EmailSendLog? GetWithContent(int emailSendLogId) =>
            this.context.EmailSendLogs
                .AsNoTracking()
                .Include(x => x.Content)
                .FirstOrDefault(x => x.EmailSendLogId == emailSendLogId);

        public int CountByContent(int emailSendLogContentId) =>
            this.context.EmailSendLogs.Count(x => x.EmailSendLogContentId == emailSendLogContentId);

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
