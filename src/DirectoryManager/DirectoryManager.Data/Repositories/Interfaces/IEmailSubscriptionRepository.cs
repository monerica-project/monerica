using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;

namespace DirectoryManager.Data.Repositories.Interfaces
{
    public interface IEmailSubscriptionRepository : IDisposable
    {
        IApplicationDbContext Context { get; }

        EmailSubscription Create(EmailSubscription model);

        bool Update(EmailSubscription model);

        EmailSubscription? Get(int emailSubscriptionId);

        EmailSubscription? Get(string email);

        IList<EmailSubscription> GetAll();

        int Total(bool subscribed = true);

        bool Delete(int emailSubscriptionId);
    }
}