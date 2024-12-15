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

        /// <summary>
        /// Gets the last sent message for the specified subscription.
        /// </summary>
        /// <param name="emailSubscriptionId">The subscription ID.</param>
        /// <returns>The last sent email record.</returns>
        SentEmailRecord? GetLastSentMessage(int emailSubscriptionId);
    }
}