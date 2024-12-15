using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class SentEmailRecordRepository : ISentEmailRecordRepository
    {
        private readonly IApplicationDbContext context;

        public SentEmailRecordRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public SentEmailRecord Create(SentEmailRecord model)
        {
            try
            {
                this.context.SentEmailRecords.Add(model);
                this.context.SaveChanges();
                return model;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public SentEmailRecord? Get(int sentEmailRecordId)
        {
            try
            {
                return this.context.SentEmailRecords.Find(sentEmailRecordId);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<SentEmailRecord> GetBySubscriptionId(int emailSubscriptionId)
        {
            try
            {
                return this.context.SentEmailRecords
                                   .Where(x => x.EmailSubscriptionId == emailSubscriptionId)
                                   .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<SentEmailRecord> GetByMessageId(int emailMessageId)
        {
            try
            {
                return this.context.SentEmailRecords
                                   .Where(x => x.EmailMessageId == emailMessageId)
                                   .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Update(SentEmailRecord model)
        {
            try
            {
                this.context.SentEmailRecords.Update(model);
                this.context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Delete(int sentEmailRecordId)
        {
            try
            {
                var entry = this.context.SentEmailRecords.Find(sentEmailRecordId);
                if (entry == null)
                {
                    return false;
                }

                this.context.SentEmailRecords.Remove(entry);
                this.context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IList<SentEmailRecord> GetAll()
        {
            try
            {
                return this.context.SentEmailRecords.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public void LogMessageDelivery(int emailSubscriptionId, int emailMessageId)
        {
            var record = new SentEmailRecord
            {
                EmailSubscriptionId = emailSubscriptionId,
                EmailMessageId = emailMessageId,
                SentDate = DateTime.UtcNow
            };
            this.context.SentEmailRecords.Add(record);
            this.context.SaveChanges();
        }

        /// <inheritdoc/>
        public SentEmailRecord? GetLastSentMessage(int emailSubscriptionId)
        {
            return this.context.SentEmailRecords
                .Where(r => r.EmailSubscriptionId == emailSubscriptionId)
                .OrderByDescending(r => r.SentDate)
                .FirstOrDefault();
        }
    }
}