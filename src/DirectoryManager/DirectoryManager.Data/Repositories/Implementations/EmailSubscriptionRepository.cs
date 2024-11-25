using DirectoryManager.Data.Constants;
using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailSubscriptionRepository : IEmailSubscriptionRepository
    {
        public EmailSubscriptionRepository(IApplicationDbContext context)
        {
            this.Context = context;
        }

        public IApplicationDbContext Context { get; private set; }

        public EmailSubscription Create(EmailSubscription model)
        {
            try
            {
                this.Context.EmailSubscriptions.Add(model);
                this.Context.SaveChanges();

                return model;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public void Dispose()
        {
            this.Context.Dispose();
        }

        public EmailSubscription? Get(int emailSubscriptionId)
        {
            try
            {
                return this.Context.EmailSubscriptions.Find(emailSubscriptionId);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public EmailSubscription? Get(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return null;
                }

                return this.Context.EmailSubscriptions.FirstOrDefault(x => x.Email == email);
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Update(EmailSubscription model)
        {
            try
            {
                this.Context.EmailSubscriptions.Update(model);
                this.Context.SaveChanges();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public bool Delete(int emailSubscriptionId)
        {
            try
            {
                var entry = this.Context.EmailSubscriptions.Find(emailSubscriptionId);

                if (entry == null)
                {
                    return false;
                }

                this.Context.EmailSubscriptions.Remove(entry);
                this.Context.SaveChanges();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public int Total(bool subscribed = true)
        {
            try
            {
                return this.Context
                           .EmailSubscriptions
                           .Where(x => x.IsSubscribed == subscribed)
                           .Count();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }

        public IList<EmailSubscription> GetAll()
        {
            try
            {
                return this.Context.EmailSubscriptions.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(StringConstants.DBErrorMessage, ex.InnerException);
            }
        }
    }
}