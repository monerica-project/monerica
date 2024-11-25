using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface ISentEmailRecordRepository
    {
        SentEmailRecord Create(SentEmailRecord model);
        SentEmailRecord? Get(int sentEmailRecordId);
        IList<SentEmailRecord> GetBySubscriptionId(int emailSubscriptionId);
        IList<SentEmailRecord> GetByMessageId(int emailMessageId);
        bool Update(SentEmailRecord model);
        bool Delete(int sentEmailRecordId);
        IList<SentEmailRecord> GetAll();
        void LogMessageDelivery(int emailSubscriptionId, int emailMessageId);
    }
}