using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailMessageRepository
    {
        EmailMessage? Get(int emailMessageId);
        IList<EmailMessage> GetAll(int pageIndex, int pageSize);
        int TotalCount();
        void Create(EmailMessage emailMessage);
        void Update(EmailMessage emailMessage);
        void Delete(int emailMessageId);
    }
}