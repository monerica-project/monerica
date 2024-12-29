using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailMessageRepository
    {
        EmailMessage? Get(int emailMessageId);
        EmailMessage? GetByKey(string emailKey);
        IList <EmailMessage> GetAll(int pageIndex, int pageSize);
        int TotalCount();
        EmailMessage Create(EmailMessage emailMessage);
        void Update(EmailMessage emailMessage);
        void Delete(int emailMessageId);
    }
}