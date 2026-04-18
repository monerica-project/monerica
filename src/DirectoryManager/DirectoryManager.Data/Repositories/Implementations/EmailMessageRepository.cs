using DirectoryManager.Data.DbContextInfo;
using DirectoryManager.Data.Models.Emails;
using DirectoryManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DirectoryManager.Data.Repositories.Implementations
{
    public class EmailMessageRepository : IEmailMessageRepository
    {
        private readonly IApplicationDbContext context;

        public EmailMessageRepository(IApplicationDbContext context)
        {
            this.context = context;
        }

        public EmailMessage? Get(int emailMessageId) =>
            this.context.EmailMessages.Find(emailMessageId);

        public IList<EmailMessage> GetAll(int pageIndex, int pageSize) =>
            this.context.EmailMessages
                .OrderBy(e => e.EmailMessageId)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

        public int TotalCount() => this.context.EmailMessages.Count();

        public EmailMessage Create(EmailMessage emailMessage)
        {
            this.context.EmailMessages.Add(emailMessage);
            this.context.SaveChanges();

            return emailMessage;
        }

        public void Update(EmailMessage emailMessage)
        {
            this.context.EmailMessages.Update(emailMessage);
            this.context.SaveChanges();
        }

        public void Delete(int emailMessageId)
        {
            var entity = this.context.EmailMessages.Find(emailMessageId);
            if (entity != null)
            {
                this.context.EmailMessages.Remove(entity);
                this.context.SaveChanges();
            }
        }

        public EmailMessage? GetByKey(string emailKey) =>
            this.context.EmailMessages
                .AsNoTracking()
                .FirstOrDefault(e => e.EmailKey == emailKey);
    }
}